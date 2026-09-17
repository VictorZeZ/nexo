using nexo.Options;
using nexo.Rooms;

namespace nexo.BackgroundServices;

/// <summary>
/// Periodically sends a heartbeat to every live connection and closes any connection that has
/// gone silent for too long. Runs independently of the connect/receive/send loops in
/// WebSocketConnection, so one slow sweep never blocks normal message handling.
/// </summary>
public sealed class StaleConnectionSweepService(
    RoomConnectionRegistry connectionRegistry,
    HeartbeatSettings settings,
    ILogger<StaleConnectionSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(settings.SweepIntervalSeconds);
        var staleAfter = TimeSpan.FromSeconds(settings.StaleAfterSeconds);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTimeOffset.UtcNow;

                foreach (var connection in connectionRegistry.GetAllConnections())
                {
                    if (now - connection.LastActivityAtUtc > staleAfter)
                    {
                        logger.LogInformation(
                            "Connection {ConnectionId} has been silent for over {StaleSeconds}s; closing as stale.",
                            connection.ConnectionId, settings.StaleAfterSeconds);
                        connection.CloseAsStale();
                        continue;
                    }

                    connection.SendHeartbeat();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }
    }
}