namespace Plml.Signaling;

public interface ILogger
{
    void Log(string category, string message);
    void Error(string category, string message);
}