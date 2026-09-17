namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "Heartbeat" configuration section.
/// Application-level connection health monitoring, distinct from the transport-level
/// WebSocket ping/pong interval configured in WebSocketConnectionSettings.
/// </summary>
public sealed class HeartbeatSettings
{
    public const string SectionName = "Heartbeat";

    /// <summary>How often the server sweeps connections for staleness.</summary>
    public int SweepIntervalSeconds { get; init; } = 20;

    /// <summary>
    /// How long a connection may go without sending any message (a heartbeat reply or otherwise)
    /// before it is considered stale and closed.
    /// </summary>
    public int StaleAfterSeconds { get; init; } = 45;
}