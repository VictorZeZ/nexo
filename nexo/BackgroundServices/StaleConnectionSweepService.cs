using nexo.Options;
using nexo.Rooms;
using nexo.WebSockets.Connection;

namespace nexo.BackgroundServices;

/// <summary>
/// Periodically maintains every live connection, including ones that have not joined a room:
/// sends a heartbeat, closes any connection that has gone silent for too long, and extends the
/// Redis TTL of the rooms and reconnect sessions the remaining connections depend on, so an
/// active room never expires underneath its participants. Runs independently of the
/// connect/receive/send loops in WebSocketConnection, so one slow sweep never blocks normal
/// message handling.
/// </summary>
public sealed class StaleConnectionSweepService(
    ConnectionRegistry connectionRegistry,
    RoomManager roomManager,
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
                var activeRoomIds = new HashSet<string>();
                var activeReconnectTokens = new List<string>();

                foreach (var connection in connectionRegistry.GetAll())
                {
                    // An exception escaping ExecuteAsync stops the whole host, so a failure while
                    // sweeping one connection must never propagate past this loop iteration.
                    try
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

                        if (connection.CurrentRoomId is { } roomId)
                        {
                            activeRoomIds.Add(roomId);
                        }

                        if (connection.ReconnectToken is { } reconnectToken)
                        {
                            activeReconnectTokens.Add(reconnectToken);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Unexpected failure while sweeping connection {ConnectionId}.", connection.ConnectionId);
                    }
                }

                await roomManager.RefreshActiveStateAsync(activeRoomIds, activeReconnectTokens, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }
    }
}