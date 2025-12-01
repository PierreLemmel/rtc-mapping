namespace Plml.RtcServer;

public delegate Task OutgoingMessageSender(OutgoingMessage message);

public interface IMessageHandler
{
    Task HandleMessage(IncomingMessage message, OutgoingMessageSender sendMessage);
}