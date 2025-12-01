namespace Plml.RtcServer;

public record MessageData;

public record LogMessageData(string message) : MessageData;
public record OfferMessageData(string sdp) : MessageData;


public record Message(string type, MessageData data);