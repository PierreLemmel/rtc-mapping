using System;

namespace Plml.Signaling;

public record IncomingMessage(string type, string data, string clientId);
public record OutgoingMessage(string type, string data, DateTime timestamp);

public record SdpOfferMessageReceived(string sdpOffer, string targetId);
public record ClientAddedMessage(string id, int count);

