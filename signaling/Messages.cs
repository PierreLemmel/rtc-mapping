using System;

namespace Plml.Signaling;

public record IncomingMessage(string type, string data, string clientId);
public record OutgoingMessage(string type, string data, DateTime timestamp);

public record SdpOfferMessageReceived(string sdpOffer, string targetId);
public record SdpAnswerMessageReceived(string sdpAnswer, string sourceId);
public record ClientAddedMessage(string id, string userName, int count);

public static class MessageTypes
{
    public const string Log = "Log";
    public const string SdpOffer = "SdpOffer";
    public const string ClientAdded = "ClientAdded";
    public const string ClientAwaiting = "ClientAwaiting";
    public const string ClientReady = "ClientReady";
    public const string SdpAnswer = "SdpAnswer";
    public const string WaitingRoom = "WaitingRoom";
}