using System.Net;
using Microsoft.Extensions.Logging;
using Plml.RtcAdapter.NDI;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;

namespace Plml.RtcAdapter;


public class RtcServerConnection : IDisposable
{
    private readonly Settings settings;
    private readonly ILogger logger;

    private NDISender ndiSender;
    private IVideoBridge videoBridge;

    private bool disposed = false;

    private string connectionId;

    private RTCPeerConnection pc;

    private string? sdpOffer;

    public event Action<string,string>? OnSdpOffer;
    public event Action<string>? OnRTCConnected;
    public event Action<string>? OnRTCDisconnected;

    public RtcServerConnection(Settings settings, string connectionId, ILogger logger)
    {
        this.settings = settings;
        this.logger = logger;
        this.connectionId = connectionId;
        

        pc = CreateNewConnection();

        ndiSender = new NDISender(Logger.Default, connectionId, [settings.NdiGroup]);
        videoBridge = new VideoBridge(logger);
    }

    public void Start()
    {
        pc.onconnectionstatechange += OnConnectionStateChange;

        pc.ondatachannel += OnDataChannel;

        pc.OnVideoFormatsNegotiated += OnVideoFormatsNegotiated;
        pc.OnAudioFormatsNegotiated += OnAudioFormatsNegotiated;

        pc.OnVideoFrameReceived += OnVideoFrameReceived;
        pc.OnAudioFrameReceived += OnAudioFrameReceived;
        
        pc.onsignalingstatechange += OnSignalingStateChange;


        MediaStreamTrack audioTrack = new(
            SDPMediaTypesEnum.audio,
            isRemote: false,
            capabilities: [new(SDPWellKnownMediaFormatsEnum.PCMU)],
            streamStatus:MediaStreamStatusEnum.RecvOnly
        );

        MediaStreamTrack videoTrack = new([
            new VideoFormat(
                VideoCodecsEnum.VP8,
                96
            ),
            new VideoFormat(
                VideoCodecsEnum.H264,
                97,
                parameters:"level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42e01f"
            )
        ], MediaStreamStatusEnum.RecvOnly);

        pc.addTrack(audioTrack);
        pc.addTrack(videoTrack);

        var offer = pc.createOffer();
        Task.Run(() => pc.setLocalDescription(offer));
    }

    private RTCPeerConnection CreateNewConnection()
    {
        RTCPeerConnection newConnection = new RTCPeerConnection(new RTCConfiguration()
        {
            iceServers = settings.IceServers.Select(server => new RTCIceServer()
            {
                urls = server
            }).ToList()
        });

        return newConnection;
    }



    private void HandleVideoFrame(IPEndPoint remoteEP, uint timestamp, byte[] frame, VideoFormat format)
    {
        try
        {
            (bool success, IEnumerable<RawImage> images, string? errorMessage) = videoBridge.Decode(remoteEP, timestamp, frame, format);
            if (!success)
            {
                logger.Error("SINC", connectionId, $"Error decoding video frame: {errorMessage ?? "Unknown error"}");
                return;
            }
            foreach (RawImage image in images)
            {
                SendImageToNDI(image, timestamp);
            }
        }
        catch (Exception ex)
        {
            logger.Error("SINC", connectionId, $"Error handling video frame: {ex.Message}");
        }
    }


    private unsafe void SendImageToNDI(RawImage image, uint timestamp)
    {
        NDILib.FourCC_video_type_e fourCC = VideoUtils.SIPToNDIPixelFormat(image.PixelFormat);
        int bpp = VideoUtils.GetBytesPerPixel(image.PixelFormat);
        int stride = VideoUtils.CalculateStride(image.Width, bpp, 8);

        int width = image.Width;
        int height = image.Height;


        var frame = new NDILib.video_frame_v2_t()
        {
            xres = image.Width,
            yres = image.Height,
            fourCC = fourCC,
            frame_rate_N = 30,
            frame_rate_D = 1,
            picture_aspect_ratio = (float)width / height,
            frame_format_type = NDILib.frame_format_type_e.progressive,
            timecode = NDILib.send_timecode_synthesize,
            p_data = image.Sample,
            line_stride_in_bytes = stride,
            timestamp = timestamp
        };

        ndiSender.SendFrame(frame);

        Console.WriteLine($"{connectionId}: Video sink decoded sample faster: {image.Width}x{image.Height} {image.PixelFormat}");
    }

    private async void OnConnectionStateChange(RTCPeerConnectionState state)
    {
        logger.Log("RTC", connectionId, $"Connection state changed: {state.ToString().ToUpper()}");
        switch (state)
        {
            case RTCPeerConnectionState.closed:
                logger.Log("RTC", connectionId, "Connection closed");
                OnRTCDisconnected?.Invoke(connectionId);
                break;
            case RTCPeerConnectionState.connected:
                logger.Log("RTC", connectionId, "Connection established");
                OnRTCConnected?.Invoke(connectionId);

                break;
            case RTCPeerConnectionState.failed:
                logger.Error("RTC", connectionId, "Connection failed");
                OnRTCDisconnected?.Invoke(connectionId);
                break;
        }
    }

    private void OnDataChannel(RTCDataChannel channel) => logger.Log("RTC", connectionId, $"Data channel opened: {channel.label}");

    private void OnVideoFormatsNegotiated(List<VideoFormat> formats) => logger.Log("RTC", connectionId, $"Video formats negotiated: {string.Join(", ", formats.Select(f => f.Codec.ToString()))}");

    private void OnAudioFormatsNegotiated(List<AudioFormat> formats) => logger.Log("RTC", connectionId, $"Audio formats negotiated: {formats.Count}");

    private void OnVideoFrameReceived(IPEndPoint remoteEP, uint timestamp, byte[] frame, VideoFormat format) => HandleVideoFrame(remoteEP, timestamp, frame, format);

    private void OnAudioFrameReceived(EncodedAudioFrame frame) => logger.Log("RTC", connectionId, $"Audio frame received: {frame.DurationMilliSeconds}");

    private void OnSignalingStateChange()
    {
        RTCSignalingState state = pc.signalingState;
        logger.Log("RTC", connectionId, $"Signaling state changed: {state.ToString().ToUpper()}");
        switch (state)
        {
            case RTCSignalingState.have_local_offer:
                if (!UpdateSdpOffer())
                {
                    logger.Error("RTC", connectionId, "Failed to update SDP offer");
                    return;
                }
                logger.Log("RTC", connectionId, "SDP offer created");
                SendSdpOffer();
                break;

            case RTCSignalingState.stable:
                break;

            case RTCSignalingState.closed:
                OnRTCDisconnected?.Invoke(connectionId);
                break;
        }
    }

    private bool UpdateSdpOffer()
    {
        if (pc.localDescription is null)
        {
            logger.Error("RTC", connectionId, "Peer connection has no local description, cannot send SDP offer");
            return false;
        }

        if (pc.localDescription.type != RTCSdpType.offer)
        {
            logger.Error("RTC", connectionId, "Local description is not an offer");
            return false;
        }

        sdpOffer = pc.localDescription.sdp.ToString();
        return true;
    }

    private void SendSdpOffer()
    {
        if (sdpOffer is null)
        {
            logger.Error("RTC", connectionId, "No SDP offer to send");
            return;
        }

        OnSdpOffer?.Invoke(connectionId, sdpOffer);
    }

    public void HandleSdpAnswerMessage(string sdp)
    {
        if (pc.RemoteDescription is not null) return;

        SDP remoteDescription = SDP.ParseSDPDescription(sdp);

        SetDescriptionResultEnum result = pc.SetRemoteDescription(SdpType.answer, remoteDescription);
        if (result != SetDescriptionResultEnum.OK)
        {
            logger.Error("RTC", connectionId, $"Failed to set remote description: {result}");
            return;
        }
        logger.Log("RTC", connectionId, "Remote description set");
    }

    public void Dispose()
    {
        if (disposed) return;

        pc?.Dispose();
        ndiSender?.Dispose();
        videoBridge?.Dispose();

        disposed = true;
    }
}