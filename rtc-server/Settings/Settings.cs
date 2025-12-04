namespace Plml.RtcServer;

public record Settings(int port, string[] iceServers, string dataChannelLabel);