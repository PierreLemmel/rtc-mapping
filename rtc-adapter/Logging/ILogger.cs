namespace Plml.RtcAdapter;

public interface ILogger
{
    void Log(string message);
    void Error(string message);
}