using Microsoft.Extensions.Logging;

namespace Plml.RtcAdapter;

public static class LoggingUtils
{
    public static void Log(this ILogger logger, string category, string message)
    {
        string fullMsg = $"[{category.ToUpper()}] {message}";
        logger.Log(LogLevel.Information, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }

    public static void Error(this ILogger logger, string category, string message)
    {
        string fullMsg = $"[{category.ToUpper()}] {message}";
        logger.Log(LogLevel.Error, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }
}