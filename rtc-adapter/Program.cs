using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plml.RtcAdapter;
using SIPSorceryMedia.FFmpeg;


ILogger logger = Logger.Default;
var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

if (!File.Exists(settingsPath))
{
    logger.Error("PROGRAM", $"Settings file not found at {settingsPath}");
    return;
}

var jsonContent = await File.ReadAllTextAsync(settingsPath);
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};
Settings settings = JsonSerializer.Deserialize<Settings>(jsonContent, options) ?? throw new JsonException("Failed to deserialize settings");



if (string.IsNullOrWhiteSpace(settings.FfmpegPath))
{
    logger.Error("PROGRAM", "Ffmpeg path is not set in the appsettings.json file");
    return;
}

FfmpegLogLevelEnum ffmpegLogLevel = settings.FfmpegLogLevel.ToLowerInvariant() switch
{
    "verbose" => FfmpegLogLevelEnum.AV_LOG_VERBOSE,
    "info" => FfmpegLogLevelEnum.AV_LOG_INFO,
    "warning" => FfmpegLogLevelEnum.AV_LOG_WARNING,
    "error" => FfmpegLogLevelEnum.AV_LOG_ERROR,
    "fatal" => FfmpegLogLevelEnum.AV_LOG_FATAL,
    "panic" => FfmpegLogLevelEnum.AV_LOG_PANIC,
    _ => FfmpegLogLevelEnum.AV_LOG_INFO,
};

logger.Log("FFMPEG", $"Initializing FFmpeg with log level: '{ffmpegLogLevel}'");
try
{
    FFmpegInit.Initialise(ffmpegLogLevel, settings.FfmpegPath, appLogger: Logger.Default);
}
catch (Exception ex)
{
    logger.Error("FFMPEG", $"Error initializing FFmpeg");
    logger.Error("FFMPEG", ex.Message);
    return;
}

logger.Log("FFMPEG", "FFmpeg initialized");

IRtcServer rtcServer = new RtcServer(settings, logger);

try
{
    await rtcServer.Start();
}
catch (Exception ex)
{
    logger.Error("PROGRAM", $"Error while running WebSocket server: {ex.Message}");
}