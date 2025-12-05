using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DataChannelDotnet;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;
using DataChannelDotnet.Impl;

namespace Plml.RtcAdapter;

public class RtcServer: IRtcServer
{
    private const string CLIENT_ID = "rtc-adapter";

    private readonly ILogger logger;
    private readonly Settings settings;
    
    private IRtcPeerConnection pc;
    private ClientWebSocket ws;
    private string? sdpOffer;

    private void ResetPeerConnection()
    {
        pc?.Dispose();
        pc = CreateNewRtcPeerConnection();
        InitializePeerConnection();

        SendSdpOffer();
    }

    private void InitializePeerConnection()
    {
        pc.OnConnectionStateChange += OnConnectionStateChange;
        pc.OnDataChannel += OnDataChannel;
        pc.OnTrack += OnTrack;
        pc.OnSignalingStateChange += OnSignalingStateChange;

        pc.CreateDataChannel(new RtcCreateDataChannelArgs()
        {
            Label = settings.dataChannelLabel,
            Protocol = RtcDataChannelProtocol.Binary
        });

    }
    private IRtcPeerConnection CreateNewRtcPeerConnection()
    {
        IRtcPeerConnection pc = new RtcPeerConnection(new RtcPeerConfiguration()
        {
            IceServers = settings.iceServers
        });

        return pc;
    }


    public RtcServer(Settings settings, ILogger logger)
    {
        this.logger = logger;
        this.settings = settings;

        ws = new ClientWebSocket();
        pc = CreateNewRtcPeerConnection();
    }

    public async Task Start()
    {
        logger.Log($"[WS] Connecting to signaling server at {settings.signalingWs}...");
        
        var uri = new Uri($"{settings.signalingWs}?clientId={CLIENT_ID}");
        await ws.ConnectAsync(uri, CancellationToken.None);
        
        logger.Log("[WS] Connected to signaling server.");

        InitializePeerConnection();

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
                logger.Error($"[WS] Error receiving message: {ex.Message}");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.Log("[WS] Server closed connection.");
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
                logger.Error($"[WS] Failed to deserialize message: {ex.Message}");
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
                logger.Log($"[WS] Unknown message type: {type}");
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

    private void OnConnectionStateChange(IRtcPeerConnection sender, rtcState state)
    {
        logger.Log($"[RTC] Connection state changed: {state}");
        switch (state)
        {
            case rtcState.RTC_CLOSED:
                logger.Log($"[RTC] Connection closed");
                ResetPeerConnection();
                logger.Log($"[RTC] Peer connection reset, ready to go");
                break;
            default:
                logger.Log($"[RTC] Connection state changed: {state}");
                break;
        }
    }

    private void OnDataChannel(IRtcPeerConnection sender, IRtcDataChannel channel)
    {
        logger.Log($"[RTC] Data channel opened: {channel.Label}");
    }

    private void OnTrack(IRtcPeerConnection sender, IRtcTrack track)
    {
        logger.Log($"[RTC] Track added: {track.Description}");
    }

    private void OnSignalingStateChange(IRtcPeerConnection sender, rtcSignalingState state)
    {
        logger.Log($"[RTC] Signaling state changed: {state}");
        switch (state)
        {
            case rtcSignalingState.RTC_SIGNALING_HAVE_LOCAL_OFFER:
                if (!UpdateSdpOffer())
                {
                    logger.Error("[RTC] Failed to update SDP offer");
                    return;
                }
                SendSdpOffer();
                logger.Log($"[RTC] SDP offer created");
                break;

            case rtcSignalingState.RTC_SIGNALING_STABLE:
                break;
            
            default:
                logger.Log($"[RTC] Unexpected Signaling state changed: {state}");
                break;
        }
    }

    private bool UpdateSdpOffer()
    {
        if (pc.LocalDescription is null)
        {
            logger.Error("[RTC] Peer connection has no local description, cannot send SDP offer");
            return false;
        }

        if (pc.LocalDescriptionType != RtcDescriptionType.Offer)
        {
            logger.Error("[RTC] Local description is not an offer");
            return false;
        }

        sdpOffer = pc.LocalDescription;
        return true;
    }

    private void SendSdpOffer()
    {
        if (sdpOffer is null)
        {
            logger.Error("[RTC] No SDP offer to send");
            return;
        }

        Task.Run(async () =>
        {
            logger.Log("[RTC] Sending SDP offer");
            await SendMessageAsync("SdpOffer", sdpOffer);
        });
    }

    private void HandleSdpAnswerMessage(string sdp)
    {
        logger.Log("[RTC] Received SDP Answer");
        pc.SetRemoteDescription(new RtcDescription()
        {
            Sdp = sdp,
            Type = RtcDescriptionType.Answer
        });
    }
}
