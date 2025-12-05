namespace Plml.Signaling;

public class Logger : ILogger
{
    public void Log(string message) => Console.WriteLine(message);
    public void Error(string message) => Console.Error.WriteLine(message);
}