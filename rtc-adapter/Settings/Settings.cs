namespace Plml.RtcAdapter;

public record Settings(int port, string[] iceServers, string dataChannelLabel, string signalingWs);