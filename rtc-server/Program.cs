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

ILogger logger = new Logger();


IRtcServer rtcServer = new RtcServer(settings, logger);

try
{
    await rtcServer.Start();
}
catch (Exception ex)
{
    logger.Error($"Error while running WebSocket server: {ex.Message}");
}

