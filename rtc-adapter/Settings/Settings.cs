namespace Plml.RtcAdapter;

public class Settings
{
    public int Port { get; init; } = 0;
    public string[] IceServers { get; init; } = ["stun:stun.l.google.com:19302"];
    public string DataChannelLabel { get; init; } = "RTC Server - Feed";
    public string SignalingWs { get; init; } = "wss://plml-signaling.fly.dev/ws";
    public required string NdiGroup { get; init; }
    public required string FfmpegPath { get; init; }
    public string FfmpegLogLevel { get; init; } = "Info";
}