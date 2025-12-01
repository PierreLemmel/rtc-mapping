namespace Plml.RtcServer;

public interface ILogger
{
    void Log(string message);
    void Error(string message);
}