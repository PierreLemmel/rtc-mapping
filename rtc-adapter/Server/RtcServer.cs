using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using Microsoft.Extensions.Logging;


namespace Plml.RtcAdapter;

public class RtcServer: IRtcServer
{
    private const string CLIENT_ID = "rtc-adapter";

    private readonly ILogger logger;
    private readonly Settings settings;
    

    private ClientWebSocket ws;
    private Dictionary<string, RtcServerConnection> connections;


    public RtcServer(Settings settings, ILogger logger)
    {
        this.logger = logger;
        this.settings = settings;

        ws = new ClientWebSocket();
        connections = new Dictionary<string, RtcServerConnection>();
    }

    private RtcServerConnection CreateNewConnection(string connectionId)
    {
        RtcServerConnection connection = new(settings, connectionId, logger);
        connection.OnSdpOffer += async (connectionId, sdpOffer) => await OnSdpOffer(connectionId, sdpOffer);

        connection.Start();

        return connection;
    }

    private async Task OnSdpOffer(string connectionId, string sdpOffer)
    {
        logger.Log("RTC", "Sending SDP offer");
        SdpOfferReadyMessage message = new(sdpOffer, connectionId);
        await SendMessageAsync("SdpOffer", message);
    }

    public async Task Start()
    {
        logger.Log("WS", $"Connecting to signaling server at {settings.SignalingWs}...");
        
        var uri = new Uri($"{settings.SignalingWs}?clientId={CLIENT_ID}");
        await ws.ConnectAsync(uri, CancellationToken.None);
        
        logger.Log("WS", "Connected to signaling server.");

        await ReceiveLoop();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[16384];
        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.Error("WS", $"Error receiving message: {ex.Message}");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.Log("WS", "Server closed connection.");
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            

            IncomingMessage? msg = null;
            try
            {
                msg = JsonSerializer.Deserialize<IncomingMessage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                logger.Error("WS", $"Failed to deserialize message: {ex.Message}");
            }

            if (msg != null)
            {
                HandleMessage(msg);
            }
        }
    }

    private void HandleMessage(IncomingMessage message)
    {
        string type = message.type;
        string data = message.data;

        switch (type)
        {
            case "SdpAnswer":
                HandleSdpAnswerMessage(data);
                break;
            
            case "ClientAwaiting":
                HandleClientAwaitingMessage(data);
                break;

            case "ClientAdded":
                HandleClientAddedMessage(data);
                break;

            default:
                logger.Log("WS", $"Unknown message type: {type}");
                break;
        }
    }

    private void HandleClientAwaitingMessage(string data)
    {
        string clientId = data;
        logger.Log("RTC", $"Client {clientId} is awaiting");

        string connectionId = clientId;
        RtcServerConnection connection = CreateNewConnection(connectionId);
        connections.Add(connectionId, connection);
    }

    private void HandleClientAddedMessage(string data)
    {
        ClientAddedMessage? clientAddedMessage = JsonSerializer.Deserialize<ClientAddedMessage>(data);

        if (clientAddedMessage is null)
        {
            logger.Error("RTC", "Failed to deserialize ClientAddedMessage");
            return;
        }

        logger.Log("RTC", clientAddedMessage.id, "Client added");
    }

    private void HandleSdpAnswerMessage(string data)
    {
        SdpAnswerReceivedMessage? message = JsonSerializer.Deserialize<SdpAnswerReceivedMessage>(data);
        if (message is null)
        {
            logger.Error("RTC", "Failed to deserialize SdpAnswerReceivedMessage");
            return;
        }

        string sdp = message.sdpAnswer;
        string sourceId = message.sourceId;

        logger.Log("RTC", sourceId, "Received SDP Answer");
        foreach (var kvp in connections)
        {
            var connection = kvp.Value;
            connection.HandleSdpAnswerMessage(sdp);
        }
    }

    private async Task SendMessageAsync(string type, string data)
    {
        if (ws.State != WebSocketState.Open) return;

        var msg = new OutgoingMessage(type, data, CLIENT_ID);
        string json = JsonSerializer.Serialize(msg);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task SendMessageAsync<TData>(string type, TData data)
    {
        string json = JsonSerializer.Serialize(data);
        await SendMessageAsync(type, json);
    }
}
