using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Plml.Signaling;

public class SignalingServer : ISignalingServer
{
    private readonly int _port;
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();

    private string? sdpOffer;
    private readonly ILogger logger;

    public SignalingServer(int port, ILogger logger)
    {
        _port = port;
        _listener = new HttpListener();
        this.logger = logger;
    }

    public async Task Start()
    {
        try
        {
            Console.WriteLine($"[SERVER] Starting Signaling server on port {_port}...");
            _listener.Prefixes.Add($"http://+:{_port}/");
            _listener.Start();
            Console.WriteLine($"[SERVER] Signaling server started on port {_port}.");
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"[SERVER] Failed to start HTTP listener on port {_port}: {ex.Message}");
            throw;
        }

        while (true)
        {
            var context = await _listener.GetContextAsync();

            if (context.Request.IsWebSocketRequest && context.Request.Url!.AbsolutePath == "/ws")
            {
                _ = HandleWebSocketAsync(context);
            }
            else
            {
                context.Response.StatusCode = 200;
                await using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync("Signaling Server.\n");
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

        if (!_clients.TryAdd(clientId, ws))
        {
            logger.Error($"[WS] Client ID {clientId} already exists");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID already exists", CancellationToken.None);
            return;
        }

        logger.Log($"[WS] Client {clientId} connected");

        await DispatchMessageAsync("ClientAdded", new ClientAddedMessage(clientId, _clients.Count));

        var buffer = new byte[16384];
        while (ws.State == WebSocketState.Open)
        {
            try {
                WebSocketReceiveResult message = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    logger.Log($"[WS] Client {clientId} requested close.");
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                    break;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, message.Count);

                IncomingMessage? msg = JsonSerializer.Deserialize<IncomingMessage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (msg is null)
                {
                    logger.Error($"[WS] Failed to deserialize message: {json}");
                    continue;
                }
                await HandleMessageAsync(msg, ws);
            }
            catch (Exception ex)
            {
                logger.Error($"[WS] Failed to receive message: {ex.Message}");
                continue;
            }
        }

        _clients.TryRemove(clientId, out _);
        logger.Log($"[WS] Client {clientId} disconnected");
        ws.Dispose();
    }


    private async Task HandleMessageAsync(IncomingMessage message, WebSocket ws)
    {
        string type = message.type;
        string data = message.data;
        string clientId = message.clientId;
        
        switch (type)
        {
            case "Log":
                HandleLogMessage(data, clientId);
                break;
            case "SdpAnswer":
                await HandleSdpAnswerMessageAsync(data);
                break;
            case "SdpOffer":
                await HandleSdpOfferMessageAsync(data);
                break;
            default:
                logger.Error($"[WS] Unknown message type: {type} from client {clientId}");
                break;
        }
    }

    private void HandleLogMessage(string message, string clientId) => logger.Log($"[WS] Log from '{clientId}': {message}");

    private async Task HandleSdpAnswerMessageAsync(string answer)
    {
        await BroadcastMessageAsync("SdpAnswer", answer);
    }

    private async Task HandleSdpOfferMessageAsync(string offer)
    {
        sdpOffer = offer;
        await DispatchMessageAsync("SdpOffer", sdpOffer);
    }

    private async Task DispatchMessageAsync<TData>(string type, TData data)
    {
        string payload = JsonSerializer.Serialize(data);
        await BroadcastMessageAsync(type, payload);
    }
    
    private async Task BroadcastMessageAsync(string type, string data)
    {
        var msg = new OutgoingMessage(type, data, DateTime.UtcNow);
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        var tasks = _clients.Select(async c => 
        {
            if (c.Value.State == WebSocketState.Open)
            {
                try
                {
                    await c.Value.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.Error($"[WS] Failed to send message to client {c.Key}: {ex.Message}");
                }
            }
        });
        
        await Task.WhenAll(tasks);
    }


}