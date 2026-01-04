using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;


namespace Plml.RtcAdapter;

public class RtcServer: IRtcServer
{
    private const string RTC_ADAPTER_CLIENT_ID = "rtc-adapter";
    private const string RTC_ADAPTER_USER_NAME = "RTC Adapter";

    private readonly ILogger logger;
    private readonly Settings settings;
    

    private readonly ClientWebSocket ws;
    private readonly Dictionary<string, RtcServerConnection> connections;


    public RtcServer(Settings settings, ILogger logger)
    {
        this.logger = logger;
        this.settings = settings;

        ws = new();
        connections = new();
    }

    private RtcServerConnection CreateNewConnection(string connectionId, string userName)
    {
        RtcServerConnection connection = new(settings, connectionId, userName, logger);
        connection.OnSdpOffer += async (connectionId, sdpOffer) => await OnSdpOffer(connectionId, sdpOffer);
        connection.OnRTCDisconnected += OnClientDisconnected;

        connection.Start();

        return connection;
    }

    private void OnClientDisconnected(string connectionId) => RemoveConnection(connectionId);

    private void RemoveConnection(string connectionId)
    {
        if (!connections.TryGetValue(connectionId, out RtcServerConnection? connection))
        {
            logger.Error("RTC", connectionId, "Impossible to remove connection: connection not found");
            return;
        }

        connections.Remove(connectionId);
        connection.Dispose();
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
        
        var uri = new Uri($"{settings.SignalingWs}?clientId={RTC_ADAPTER_CLIENT_ID}&userName={RTC_ADAPTER_USER_NAME}");
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
        ClientAwaitingMessage? clientAwaitingMessage = JsonSerializer.Deserialize<ClientAwaitingMessage>(data);

        if (clientAwaitingMessage is null)
        {
            logger.Error("RTC", "Failed to deserialize ClientAwaitingMessage");
            return;
        }

        (string clientId, string userName) = clientAwaitingMessage; 
        logger.Log("RTC", $"Client {clientId} is awaiting with user name '{userName}'");

        RtcServerConnection connection = CreateNewConnection(clientId, userName);
        connections.Add(clientId, connection);
    }

    private void HandleClientAddedMessage(string data)
    {
        ClientAddedMessage? clientAddedMessage = JsonSerializer.Deserialize<ClientAddedMessage>(data);

        if (clientAddedMessage is null)
        {
            logger.Error("RTC", "Failed to deserialize ClientAddedMessage");
            return;
        }

        logger.Log("RTC", clientAddedMessage.id, $"Client added with user name '{clientAddedMessage.userName}'");
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

        if (!connections.TryGetValue(sourceId, out RtcServerConnection? connection))
        {
            logger.Error("RTC", sourceId, "Received SDP Answer from unknown client");
            return;
        }

        connection.HandleSdpAnswerMessage(sdp);
    }

    private async Task SendMessageAsync(string type, string data)
    {
        if (ws.State != WebSocketState.Open) return;

        var msg = new OutgoingMessage(type, data, RTC_ADAPTER_CLIENT_ID);
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
