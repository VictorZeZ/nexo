using System.Collections.Concurrent;

namespace nexo.Rooms;

/// <summary>
/// Tracks in-memory, cancellable delayed removals for participants whose WebSocket connection
/// has dropped, giving them a grace period to reconnect before they are actually removed from
/// their room. Process-local: if the server restarts mid-grace-period, the pending removal is
/// lost and the participant's room membership persists until the room's own TTL backstop expires
/// instead — an accepted limitation given this runs as a single instance (see RoomConnectionRegistry).
/// </summary>
public sealed class PendingRoomRemovalTracker
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRemovals = new();

    /// <summary>Schedules <paramref name="onExpired"/> to run after <paramref name="gracePeriod"/>, unless cancelled first.</summary>
    public void ScheduleRemoval(string participantId, TimeSpan gracePeriod, Func<Task> onExpired)
    {
        if (_pendingRemovals.TryRemove(participantId, out var previous))
        {
            // A removal was already pending for this participant (unlikely, but handled rather
            // than assumed away); cancel it so only one timer is ever running per participant.
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource();
        _pendingRemovals[participantId] = cts;

        _ = RunAfterDelayAsync(participantId, gracePeriod, cts, onExpired);
    }

    /// <summary>Cancels a pending removal, e.g. because the participant reconnected in time.</summary>
    public void CancelPendingRemoval(string participantId)
    {
        if (_pendingRemovals.TryRemove(participantId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task RunAfterDelayAsync(string participantId, TimeSpan gracePeriod, CancellationTokenSource cts, Func<Task> onExpired)
    {
        try
        {
            await Task.Delay(gracePeriod, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (_pendingRemovals.TryGetValue(participantId, out var current) && current == cts)
            {
                _pendingRemovals.TryRemove(participantId, out _);
            }

            cts.Dispose();
        }

        await onExpired();
    }
}