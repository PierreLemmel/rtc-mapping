using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Plml.RtcServer;

public class WebSocketServer: IWebSocketServer
{
    private readonly ILogger logger;
    private readonly IMessageHandler messageHandler;
    private readonly HttpListener listener;
    private readonly int port;
    private readonly ConcurrentDictionary<string, WebSocket> clients = new();
    
    public WebSocketServer(ILogger logger, IMessageHandler messageHandler, int port)
    {
        this.logger = logger;
        this.messageHandler = messageHandler;
        this.port = port;
        this.listener = new HttpListener();
    }

    public async Task Start()
    {
        try
        {
            logger.Log($"Starting RTC Mappingserver on port {port}...");
            listener.Prefixes.Add($"http://+:{port}/");
            listener.Start();
            logger.Log($"RTC Mapping server started on port {port}.");
        }
        catch (HttpListenerException ex)
        {
            logger.Error($"Failed to start HTTP listener on port {port}: {ex.Message}");
            logger.Error("On Windows, you may need to run as administrator or reserve the URL.");
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
                await writer.WriteAsync("This is a simple WebSocket/HTTP server.\n");
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
            logger.Error("Client ID is required");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID is required", CancellationToken.None);
            return;
        }

        if (!clients.TryAdd(clientId, ws))
        {
            logger.Error($"Client ID {clientId} already exists");
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Client ID already exists", CancellationToken.None);
            return;
        }

        logger.Log($"WebSocket connection established. Client ID: {clientId}");

        var buffer = new byte[16384];
        while (ws.State == WebSocketState.Open)
        {
            var message = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (message.MessageType == WebSocketMessageType.Close)
            {
                logger.Log($"Client {clientId} requested close.");
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                break;
            }
            
            string json = Encoding.UTF8.GetString(buffer, 0, message.Count);
            IncomingMessage? msg = JsonSerializer.Deserialize<IncomingMessage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (msg is null)
            {
                logger.Error($"Failed to deserialize message: {json}");
                continue;
            }
            await messageHandler.HandleMessage(msg, outMsg => SendOutgoingMessageAsync(ws, outMsg));
        }

        clients.TryRemove(clientId, out _);
        logger.Log($"WebSocket connection closed. Client ID: {clientId}");
        
        ws.Dispose();
    }

    private static async Task SendOutgoingMessageAsync(WebSocket ws, OutgoingMessage message, CancellationToken? cancellationToken = null)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken ?? CancellationToken.None);
    }
}