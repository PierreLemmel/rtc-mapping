namespace Plml.RtcAdapter;

public record OutgoingMessage(string type, string data, string clientId);
public record IncomingMessage(string type, string data, DateTime timestamp);


public record ClientAddedMessage(string id, int count);