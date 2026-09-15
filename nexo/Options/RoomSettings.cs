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
}