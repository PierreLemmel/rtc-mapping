using Plml.Signaling;

int port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out int p) ? p : 8080;

ILogger logger = new Logger();
ISignalingServer server = new SignalingServer(port, logger);

try
{
    await server.Start();
}
catch (Exception ex)
{
    logger.Error($"[PROGRAM] Error while running Signaling server: {ex.Message}");
}
