using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Linq;

namespace Plml.Signaling;

public class SignalingServer : ISignalingServer
{
    private const string RTC_ADAPTER_CLIENT_ID = "rtc-adapter";

    private readonly int _port;
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, WebSocket> clients = new();

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
                _ = HandleHttpRequestAsync(context);
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
            logger.Error("WS", "Client ID is required");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID is required", CancellationToken.None);
            return;
        }

        if (!clients.TryAdd(clientId, ws))
        {
            logger.Error("WS", $"Client ID {clientId} already exists");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID already exists", CancellationToken.None);
            return;
        }

        logger.Log("WS", $"Client {clientId} connected");

        await Task.Delay(100);
        await OnClientAddedAsync(clientId);

        var buffer = new byte[16384];
        while (ws.State == WebSocketState.Open)
        {
            try {
                WebSocketReceiveResult message = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    logger.Log("WS", $"Client {clientId} requested close.");
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
                    logger.Error("WS", $"Failed to deserialize message: {json}");
                    continue;
                }
                await HandleMessageAsync(msg, ws);
            }
            catch (Exception ex)
            {
                logger.Error("WS", $"Failed to receive message: {ex.Message}");
                continue;
            }
        }

        clients.TryRemove(clientId, out _);
        logger.Log("WS", $"Client {clientId} disconnected");
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
            case "WaitingRoom":
                await HandleWaitingRoomMessageAsync(data, clientId);
                break;
            case "ClientConnected":
                HandleClientConnectedMessage(data, clientId);
                break;  
            case "SdpAnswer":
                await HandleSdpAnswerMessageAsync(data, clientId);
                break;
            case "SdpOffer":
                await HandleSdpOfferMessageAsync(data, clientId);
                break;
            default:
                logger.Error("WS", $"Unknown message type: {type} from client {clientId}");
                break;
        }
    }

    private void HandleLogMessage(string message, string clientId) => logger.Log("WS", $"Log from '{clientId}': {message}");

    private HashSet<string> waitingRoom = new();
    private async Task HandleWaitingRoomMessageAsync(string data, string clientId)
    {
        waitingRoom.Add(clientId);
        logger.Log("WS", $"Client {clientId} added to waiting room");
        await SendMessageAsync(RTC_ADAPTER_CLIENT_ID, "ClientAwaiting", clientId);
    }

    private void HandleClientConnectedMessage(string data, string clientId)
    {
        waitingRoom.Remove(clientId);
        logger.Log("WS", $"Client {clientId} connected and removed from waiting room");
    }

    private async Task HandleSdpAnswerMessageAsync(string answer, string clientId)
    {
        logger.Log("RTC", $"Received SDP answer from client {clientId}");
        await SendMessageAsync(RTC_ADAPTER_CLIENT_ID, "SdpAnswer", answer);
    }

    private async Task HandleSdpOfferMessageAsync(string offer, string clientId)
    {
        if (clientId != RTC_ADAPTER_CLIENT_ID)
        {
            logger.Error("RTC", "Only the RTC adapter can send SDP offers");
            return;
        }

        logger.Log("RTC", "SDP offer received from adapter");
        await BroadcastMessageAsync("SdpOffer", offer, excludeClientIds: [RTC_ADAPTER_CLIENT_ID]);
    }
    private async Task SendMessageAsync(string clientId, string type, string data)
    {
        if (!clients.TryGetValue(clientId, out var ws))
        {
            logger.Error("WS", $"Client {clientId} not found");
            return;
        }

        if (ws.State != WebSocketState.Open)
        {
            logger.Error("WS", $"Client {clientId} is not open");
            return;
        }

        var msg = new OutgoingMessage(type, data, DateTime.UtcNow);
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.Error("WS", $"Failed to send message to client {clientId}: {ex.Message}");
        }
    }

    private async Task BroadcastMessageAsync(string type, string data, string[]? excludeClientIds = null)
    {
        var msg = new OutgoingMessage(type, data, DateTime.UtcNow);
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);


        var tasks = (excludeClientIds is null ? clients : clients.Where(c => !excludeClientIds.Contains(c.Key)))
        .Select(async c => 
        {
            await c.Value.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        });
        
        await Task.WhenAll(tasks);
    }

    private async Task BroadcastMessageAsync<TData>(string type, TData data)
    {
        string payload = JsonSerializer.Serialize(data);
        await BroadcastMessageAsync(type, payload);
    }

    private async Task OnClientAddedAsync(string clientId)
    {
        await BroadcastMessageAsync("ClientAdded", new ClientAddedMessage(clientId, clients.Count));
    }

    private async Task HandleHttpRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url!.AbsolutePath;

        try
        {
            if (path == "/" || path == "/health")
            {
                await HandleHealthEndpointAsync(response);
            }
            else if (path.StartsWith("/api/"))
            {
                await HandleApiRequestAsync(request, response, path);
            }
            else
            {
                response.StatusCode = 404;
                await WriteJsonResponseAsync(response, new { error = "Not Found" });
            }
        }
        catch (Exception ex)
        {
            logger.Error("HTTP", $"Error handling HTTP request: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonResponseAsync(response, new { error = "Internal Server Error" });
        }
        finally
        {
            response.Close();
        }
    }

    private async Task HandleApiRequestAsync(HttpListenerRequest request, HttpListenerResponse response, string path)
    {
        response.ContentType = "application/json";
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            return;
        }

        switch (path)
        {
            case "/api/waiting-room":
                await HandleWaitingRoomEndpointAsync(response);
                break;
            default:
                response.StatusCode = 404;
                await WriteJsonResponseAsync(response, new { error = "Endpoint not found" });
                break;
        }
    }

    private async Task HandleHealthEndpointAsync(HttpListenerResponse response)
    {
        response.ContentType = "application/json";
        response.StatusCode = 200;
        await WriteJsonResponseAsync(response, new
        {
            status = "ok",
            service = "Signaling Server",
            timestamp = DateTime.UtcNow
        });
    }

    private async Task HandleWaitingRoomEndpointAsync(HttpListenerResponse response)
    {
        response.ContentType = "application/json";
        response.StatusCode = 200;
        await WriteJsonResponseAsync(response, new { clients = waitingRoom.ToArray() });
    }

    private async Task WriteJsonResponseAsync(HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
    }
}