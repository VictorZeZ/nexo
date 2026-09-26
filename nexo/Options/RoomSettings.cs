namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "Rooms" configuration section.
/// </summary>
public sealed class RoomSettings
{
    public const string SectionName = "Rooms";

    /// <summary>
    /// Maximum number of participants allowed in a room, enforced atomically at join time.
    /// </summary>
    public int DefaultMaxParticipants { get; init; } = 50;

    /// <summary>
    /// How long a room's Redis state (and its chat history) survives without activity before it
    /// expires automatically. Refreshed on every successful join.
    /// </summary>
    public int TimeToLiveMinutes { get; init; } = 60;

    /// <summary>
    /// Maximum number of chat messages retained per room's history in Redis. Older messages are
    /// trimmed once this limit is exceeded, so history storage can never grow unbounded.
    /// </summary>
    public int MaxHistoryMessages { get; init; } = 200;

    /// <summary>
    /// How long a participant's seat is held after an unexpected disconnect, as long as at least
    /// one other participant is still connected. If nobody else remains connected, removal (and
    /// room deletion, if now empty) happens immediately instead of waiting out this period.
    /// </summary>
    public int ReconnectGracePeriodSeconds { get; init; } = 1800;

    /// <summary>
    /// Maximum number of rooms a single connection may create. A coarse, cheap defense against
    /// mass room creation; combined with per-IP connection and message-rate limits.
    /// </summary>
    public int MaxRoomsCreatedPerConnection { get; init; } = 10;
}