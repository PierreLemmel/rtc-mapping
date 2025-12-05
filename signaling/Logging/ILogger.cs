namespace Plml.Signaling;

public interface ILogger
{
    void Log(string message);
    void Error(string message);
}