using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using Microsoft.Extensions.Logging;


namespace Plml.RtcAdapter;

public class RtcServer: IRtcServer
{
    private const string CLIENT_ID = "rtc-adapter";

    private readonly ILogger logger;
    private readonly Settings settings;
    
    private RTCPeerConnection pc;
    private ClientWebSocket ws;
    private string? sdpOffer;

    private Dictionary<string, RtcServerConnection> connections;

    private async Task ResetPeerConnection()
    {
        pc?.Dispose();
        pc = CreateNewConnection();

        await InitializePeerConnection();

        SendSdpOffer();
    }

    private async Task InitializePeerConnection()
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
        await pc.setLocalDescription(offer);
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


    public RtcServer(Settings settings, ILogger logger)
    {
        this.logger = logger;
        this.settings = settings;

        ws = new ClientWebSocket();
        pc = CreateNewConnection();
        connections = new Dictionary<string, RtcServerConnection>() {
            { "default", new RtcServerConnection(settings, "default", logger) }
        };
    }

    public async Task Start()
    {
        logger.Log("WS", $"Connecting to signaling server at {settings.SignalingWs}...");
        
        var uri = new Uri($"{settings.SignalingWs}?clientId={CLIENT_ID}");
        await ws.ConnectAsync(uri, CancellationToken.None);
        
        logger.Log("WS", "Connected to signaling server.");

        await InitializePeerConnection();

        await ReceiveLoop();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[16384];
        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.Error("WS", $"Error receiving message: {ex.Message}");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.Log("WS", "Server closed connection.");
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            

            IncomingMessage? msg = null;
            try
            {
                msg = JsonSerializer.Deserialize<IncomingMessage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                logger.Error("WS", $"Failed to deserialize message: {ex.Message}");
            }

            if (msg != null)
            {
                HandleMessage(msg);
            }
        }
    }

    private void HandleMessage(IncomingMessage message)
    {
        string type = message.type;
        string data = message.data;

        switch (type)
        {
            case "SdpAnswer":
                HandleSdpAnswerMessage(data);
                break;
            
            case "ClientAdded":
            case "SdpOffer":
                break;

            default:
                logger.Log("WS", $"Unknown message type: {type}");
                break;
        }
    }

    private async Task SendMessageAsync(string type, string data)
    {
        if (ws.State != WebSocketState.Open) return;

        var msg = new OutgoingMessage(type, data, CLIENT_ID);
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async void OnConnectionStateChange(RTCPeerConnectionState state)
    {
        logger.Log("RTC", $"Connection state changed: {state.ToString().ToUpper()}");
        switch (state)
        {
            case RTCPeerConnectionState.closed:
                logger.Log("RTC", "Connection closed");
                await ResetPeerConnection();
                logger.Log("RTC", "Peer connection reset, ready to go");
                break;
            case RTCPeerConnectionState.connected:
                logger.Log("RTC", "Connection established");

                uint localSsrc = pc.VideoLocalTrack.Ssrc;
                uint remoteSsrc = pc.VideoRemoteTrack.Ssrc;
                RTCPFeedback pliFeedback = new(localSsrc, remoteSsrc, PSFBFeedbackTypesEnum.PLI);
                pc.SendRtcpFeedback(SDPMediaTypesEnum.video, pliFeedback);
                
                break;
            case RTCPeerConnectionState.failed:
                logger.Error("RTC", "Connection failed");
                await ResetPeerConnection();
                logger.Log("RTC", "Peer connection reset");
                break;
        }
    }

    private void OnDataChannel(RTCDataChannel channel)
    {
        logger.Log("RTC", $"Data channel opened: {channel.label}");
    }

    private async void OnVideoFormatsNegotiated(List<VideoFormat> formats)
    {
        logger.Log("RTC", $"Video formats negotiated: {string.Join(", ", formats.Select(f => f.Codec.ToString()))}");
        var connection = connections["default"];
        await connection.SetVideoSinkFormat(formats[0]);
    }

    private void OnAudioFormatsNegotiated(List<AudioFormat> formats)
    {
        logger.Log("RTC", $"Audio formats negotiated: {formats.Count}");
    }

    private void OnVideoFrameReceived(IPEndPoint remoteEP, uint timestamp, byte[] frame, VideoFormat format)
    {
        var connection = connections["default"];
        connection.HandleVideoFrame(remoteEP, timestamp, frame, format);
    }

    private void OnAudioFrameReceived(EncodedAudioFrame frame)
    {
        logger.Log("RTC", $"Audio frame received: {frame.DurationMilliSeconds}");
    }

    private void OnSignalingStateChange()
    {
        RTCSignalingState state = pc.signalingState;
        logger.Log("RTC", $"Signaling state changed: {state.ToString().ToUpper()}");
        switch (state)
        {
            case RTCSignalingState.have_local_offer:
                if (!UpdateSdpOffer())
                {
                    logger.Error("RTC", "Failed to update SDP offer");
                    return;
                }
                SendSdpOffer();
                logger.Log("RTC", "SDP offer created");
                break;

            case RTCSignalingState.stable:
                break;
        }
    }

    private bool UpdateSdpOffer()
    {
        if (pc.localDescription is null)
        {
            logger.Error("RTC", "Peer connection has no local description, cannot send SDP offer");
            return false;
        }

        if (pc.localDescription.type != RTCSdpType.offer)
        {
            logger.Error("RTC", "Local description is not an offer");
            return false;
        }

        sdpOffer = pc.localDescription.sdp.ToString();
        return true;
    }

    private void SendSdpOffer()
    {
        if (sdpOffer is null)
        {
            logger.Error("RTC", "No SDP offer to send");
            return;
        }

        Task.Run(async () =>
        {
            logger.Log("RTC", "Sending SDP offer");
            await SendMessageAsync("SdpOffer", sdpOffer);
        });
    }

    private void HandleSdpAnswerMessage(string sdp)
    {
        logger.Log("RTC", "Received SDP Answer");
        SDP remoteDescription = SDP.ParseSDPDescription(sdp);
        SetDescriptionResultEnum result = pc.SetRemoteDescription(SdpType.answer, remoteDescription);
        if (result != SetDescriptionResultEnum.OK)
        {
            logger.Error("RTC", $"Failed to set remote description: {result}");
            return;
        }
        logger.Log("RTC", "Remote description set");
    }
}
