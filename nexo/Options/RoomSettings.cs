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
    /// How long a room's Redis state survives without activity before it expires automatically.
    /// Refreshed on every successful join. Acts as a backstop for rooms nobody explicitly leaves.
    /// </summary>
    public int TimeToLiveMinutes { get; init; } = 60;
}