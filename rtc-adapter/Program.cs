using System.Runtime.InteropServices;
using System.Text.Json;
using Plml.RtcAdapter;

using Plml.RtcAdapter.NDI;


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


int w = 100;
int h = 100;

int id = new Random().Next(0, 999);
using (INDISender sender = new NDISender(logger, $"NDI Sender - Test {id:000}", ["Test", "Plml"]))
using (NDIVideoFrame frame = new NDIVideoFrame(w, h))
{
    unsafe 
    {
        NativeMemory.Fill(frame.Data, (nuint)(w * h * 4), (byte)255);
    }

    while (true)
    {
        sender.SendFrame(frame);
        await Task.Delay(1000 / 30);
    }
}


// IRtcServer rtcServer = new RtcServer(settings, logger);

// try
// {
//     await rtcServer.Start();
// }
// catch (Exception ex)
// {
//     logger.Error("PROGRAM", $"Error while running WebSocket server: {ex.Message}");
// }

