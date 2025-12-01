namespace Plml.RtcServer;

public interface IMessageHandler
{
    void HandleMessage(Message message);
}