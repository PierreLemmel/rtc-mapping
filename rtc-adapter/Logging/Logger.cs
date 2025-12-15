namespace Plml.RtcAdapter;

public class Logger : ILogger
{
    public void Log(string category, string message) => Console.WriteLine($"[{category}] {message}");
    public void Error(string category, string message) => Console.Error.WriteLine($"[{category}] {message}");
}