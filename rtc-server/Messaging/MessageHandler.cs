namespace Plml.RtcServer;

public class MessageHandler : IMessageHandler
{
    private readonly ILogger logger;

    public MessageHandler(ILogger logger)
    {
        this.logger = logger;
    }

    public void HandleMessage(Message message)
    {
        switch (message)
        {
            case Message("log", LogMessageData logMessageData):
                HandleLogMessage(logMessageData);
                break;
            case Message("offer", OfferMessageData offerMessageData):
                HandleOfferMessage(offerMessageData);
                break;
            default:
                throw new InvalidOperationException($"Unknown message type: {message.type}");
        }
    }

    private void HandleLogMessage(LogMessageData logMessageData)
    {
        logger.Log($"Received log message:");
        logger.Log(logMessageData.message);
    }

    private void HandleOfferMessage(OfferMessageData offerMessageData)
    {
        logger.Log(offerMessageData.sdp);
    }
}