using Microsoft.Extensions.Logging;

namespace Plml.RtcAdapter;

public static class LoggingUtils
{
    public static void Log(this ILogger logger, string category, string message)
    {
        string fullMsg = FormatMessage(category, message);
        logger.Log(LogLevel.Information, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }

    public static void Warn(this ILogger logger, string category, string message)
    {
        string fullMsg = FormatMessage(category, message);
        logger.Log(LogLevel.Warning, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }

    public static void Error(this ILogger logger, string category, string message)
    {
        string fullMsg = FormatMessage(category, message);
        logger.Log(LogLevel.Error, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }

    public static void Log(this ILogger logger, string category, string clientId, string message)
    {
        string fullMsg = FormatMessage(category, clientId, message);
        logger.Log(LogLevel.Information, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }

    public static void Warn(this ILogger logger, string category, string clientId, string message)
    {
        string fullMsg = FormatMessage(category, clientId, message);
        logger.Log(LogLevel.Warning, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }

    public static void Error(this ILogger logger, string category, string clientId, string message)
    {
        string fullMsg = FormatMessage(category, clientId, message);
        logger.Log(LogLevel.Error, new EventId(0), fullMsg, null, (state, exception) => state.ToString());
    }


    private static string FormatMessage(string category, string message)
    {
        return $"[{category.ToUpper()}] {message}";
    }

    private static string FormatMessage(string category, string clientId, string message)
    {
        return $"[{category.ToUpper()}] ({clientId}) {message}";
    }
}