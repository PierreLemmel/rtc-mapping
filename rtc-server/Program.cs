using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Plml.RtcServer;

var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

if (!File.Exists(settingsPath))
{
    throw new FileNotFoundException($"Settings file not found at {settingsPath}");
}

var jsonContent = await File.ReadAllTextAsync(settingsPath);
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};
Settings settings = JsonSerializer.Deserialize<Settings>(jsonContent, options) ?? throw new JsonException("Failed to deserialize settings");

var port = settings.port;

ILogger logger = new Logger();
IMessageHandler msgHandler = new MessageHandler(logger);

var listener = new HttpListener();
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

async Task HandleWebSocketAsync(HttpListenerContext context)
{
    var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
    using var ws = webSocketContext.WebSocket;

    logger.Log("WebSocket connection established");

    var buffer = new byte[4096];
    while (ws.State == WebSocketState.Open)
    {
        var message = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (message.MessageType == WebSocketMessageType.Close)
        {
            logger.Log("Client requested close.");
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
            break;
        }
        
        string json = Encoding.UTF8.GetString(buffer, 0, message.Count);
        Message? msg = JsonSerializer.Deserialize<Message>(json, options);
        if (msg is null)
        {
            logger.Error($"Failed to deserialize message: {json}");
            continue;
        }
        msgHandler.HandleMessage(msg);
    }

    logger.Log("WebSocket connection closed");
}