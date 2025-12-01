namespace Plml.RtcServer;

public record IncomingMessage(string type, string data, string clientId);
public record OutgoingMessage(string type, string data);