using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using DataChannelDotnet;
using DataChannelDotnet.Data;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Events;
using DataChannelDotnet.Impl;

namespace Plml.RtcServer;

public class RtcServer: IRtcServer
{
    private readonly ILogger logger;
    private readonly HttpListener listener;
    private readonly Settings settings;
    private readonly ConcurrentDictionary<string, WebSocket> clients = new();
    
    private IRtcPeerConnection pc;
    private string? sdpOffer;

    private void ResetPeerConnection()
    {
        pc?.Dispose();
        pc = CreateNewRtcPeerConnection();

        DispatchSdpOffer();
    }

    private IRtcPeerConnection CreateNewRtcPeerConnection()
    {
        IRtcPeerConnection pc = new RtcPeerConnection(new RtcPeerConfiguration()
        {
            IceServers = settings.iceServers
        });

        pc.OnConnectionStateChange += OnConnectionStateChange;
        pc.OnDataChannel += OnDataChannel;
        pc.OnTrack += OnTrack;
        pc.OnSignalingStateChange += OnSignalingStateChange;

        pc.CreateDataChannel(new RtcCreateDataChannelArgs()
        {
            Label = settings.dataChannelLabel,
            Protocol = RtcDataChannelProtocol.Binary
        });

        return pc;
    }

    public RtcServer(Settings settings, ILogger logger)
    {
        this.logger = logger;
        this.settings = settings;
        
        pc = CreateNewRtcPeerConnection();
        listener = new HttpListener();
    }

    public async Task Start()
    {
        int port = settings.port;

        try
        {
            logger.Log($"[SERVER] Starting RTC Mappingserver on port {port}...");
            listener.Prefixes.Add($"http://+:{port}/");
            listener.Start();
            logger.Log($"[SERVER] RTC Mapping server started on port {port}.");
        }
        catch (HttpListenerException ex)
        {
            logger.Error($"[SERVER] Failed to start HTTP listener on port {port}: {ex.Message}");
            logger.Error("[SERVER] On Windows, you may need to run as administrator or reserve the URL.");
            throw;
        }

        while (true)
        {
            var context = await listener.GetContextAsync();

            if (context.Request.IsWebSocketRequest && context.Request.Url!.AbsolutePath == "/ws")
            {
                _ = HandleWebSocketAsync(context);
            }
            else
            {
                context.Response.StatusCode = 200;
                await using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync("RTC Mapping Server.\n");
                context.Response.Close();
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
        var ws = webSocketContext.WebSocket;

        string? clientId = context.Request.QueryString.Get("clientId");
        if (clientId is null)
        {
            logger.Error("[WS] Client ID is required");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID is required", CancellationToken.None);
            return;
        }

        if (!clients.TryAdd(clientId, ws))
        {
            logger.Error($"[WS] Client ID {clientId} already exists");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID already exists", CancellationToken.None);
            return;
        }

        logger.Log($"[WS] Client {clientId} connected");
        
        await Task.Delay(100);
        await OnClientAddedAsync(clientId);

        var buffer = new byte[16384];
        while (ws.State == WebSocketState.Open)
        {
            var message = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (message.MessageType == WebSocketMessageType.Close)
            {
                logger.Log($"[WS] Client {clientId} requested close.");
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                break;
            }
            
            string json = Encoding.UTF8.GetString(buffer, 0, message.Count);
            IncomingMessage? msg = JsonSerializer.Deserialize<IncomingMessage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (msg is null)
            {
                logger.Error($"[WS] Failed to deserialize message: {json}");
                continue;
            }
            await HandleMessageAsync(msg, ws);
        }

        clients.TryRemove(clientId, out _);
        logger.Log($"[WS] Client {clientId} disconnected");
        
        ws.Dispose();
    }

    private async Task OnClientAddedAsync(string clientId)
    {
        await DispatchMessageAsync("ClientAdded", new ClientAddedMessage(clientId, clients.Count));
        if (sdpOffer is not null)
        {
            await SendOutgoingMessageAsync(clientId, "SdpOffer", sdpOffer);
        }
    }

    private async Task SendOutgoingMessageAsync(string clientId, string type, string data, CancellationToken? cancellationToken = null)
    {
        if (!clients.TryGetValue(clientId, out var ws))
        {
            logger.Error($"[WS] Client {clientId} not found");
            return;
        }
        await SendOutgoingMessageAsync(ws, type, data, cancellationToken);
    }

    private async Task SendOutgoingMessageAsync(WebSocket ws, string type, string data, CancellationToken? cancellationToken = null)
    {
        string json = JsonSerializer.Serialize(new OutgoingMessage(type, data, DateTime.UtcNow));
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken ?? CancellationToken.None);
    }

    private async Task DispatchMessageAsync(string type, string data, CancellationToken? cancellationToken = null)
    {
        await Task.WhenAll(clients.Values.Select(async ws => await SendOutgoingMessageAsync(ws, type, data, cancellationToken)));
    }

    private async Task DispatchMessageAsync<TData>(string type, TData data, CancellationToken? cancellationToken = null)
    {
        string payload = JsonSerializer.Serialize(data);
        await DispatchMessageAsync(type, payload, cancellationToken);
    }

    private async Task HandleMessageAsync(IncomingMessage message, WebSocket ws)
    {
        string type = message.type;
        string data = message.data;
        string clientId = message.clientId;
        
        switch (type)
        {
            case "Log":
                {
                    HandleLogMessage(data, clientId);
                }
                break;
            case "SdpAnswer":
                {
                    HandleSdpAnswerMessage(data);
                }
                break;
            default:
                logger.Error($"Unknown message type: {type} from client {clientId}");
                break;
        }
    }

    private void HandleLogMessage(string message, string clientId)
    {
        logger.Log($"Log from '{clientId}':");
        logger.Log(message);
    }

    private void HandleSdpAnswerMessage(string sdp) =>
        pc.SetRemoteDescription(new RtcDescription()
        {
            Sdp = sdp,
            Type = RtcDescriptionType.Answer
        });

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
        switch (state)
        {
            case rtcSignalingState.RTC_SIGNALING_HAVE_LOCAL_OFFER:
                DispatchSdpOffer();
                logger.Log($"[RTC] SDP offer created");
                break;

            case rtcSignalingState.RTC_SIGNALING_STABLE:
                break;
            
            default:
                logger.Log($"[RTC] Unexpected Signaling state changed: {state}");
                break;
        }
        logger.Log($"[RTC] Signaling state changed: {state}");
    }

    private void DispatchSdpOffer()
    {
        if (pc.LocalDescription is null)
        {
            logger.Error("[RTC] Missing SDP offer");
            return;
        }

        if (pc.LocalDescriptionType != RtcDescriptionType.Offer)
        {
            logger.Error("[RTC] Local description is not an offer");
            return;
        }

        sdpOffer = pc.LocalDescription;
        Task.Run(async () => await DispatchMessageAsync("SdpOffer", sdpOffer));
    }
}