namespace Plml.RtcServer;

public class MessageHandler : IMessageHandler
{
    private readonly ILogger logger;

    public MessageHandler(ILogger logger)
    {
        this.logger = logger;
    }

    public async Task HandleMessage(IncomingMessage message, OutgoingMessageSender sendMessage)
    {
        string type = message.type;
        string data = message.data;
        string clientId = message.clientId;
        
        switch (type)
        {
            case "log":
                HandleLogMessage(data, clientId);
                break;
            case "offer":
                HandleOfferMessage(data, clientId);
                break;
            default:
                logger.Error($"Unknown message type: {type} from client {clientId}");
                break;
        }
    }

    private void HandleLogMessage(string message, string clientId)
    {
        logger.Log($"Log from '{clientId}':");
        logger.Log(message);
    }

    private void HandleOfferMessage(string offer, string clientId)
    {
        logger.Log($"Received offer from client {clientId}:");
        logger.Log(offer);
    }
}