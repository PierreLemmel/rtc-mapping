namespace Plml.RtcAdapter;

public record OutgoingMessage(string type, string data, string clientId);
public record IncomingMessage(string type, string data, DateTime timestamp);

public record SdpOfferReadyMessage(string sdpOffer, string targetId);
public record SdpAnswerReceivedMessage(string sdpAnswer, string sourceId);

public record ClientAddedMessage(string id, string userName, int count);