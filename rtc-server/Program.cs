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

IWebSocketServer webSocketServer = new WebSocketServer(logger, msgHandler, port);

try
{
    await webSocketServer.Start();
}
catch (Exception ex)
{
    logger.Error($"Error while running WebSocket server: {ex.Message}");
}

