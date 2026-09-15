namespace nexo.Rooms.Models;

/// <summary>
/// The outcome of a join attempt. Exactly one of a successful <see cref="Visibility"/> or a
/// failure <see cref="Outcome"/> other than <see cref="RoomJoinOutcome.Joined"/> is meaningful.
/// </summary>
public sealed class RoomJoinResult
{
    public RoomJoinOutcome Outcome { get; }
    public RoomVisibility? Visibility { get; }

    public bool IsSuccess => Outcome == RoomJoinOutcome.Joined;

    private RoomJoinResult(RoomJoinOutcome outcome, RoomVisibility? visibility)
    {
        Outcome = outcome;
        Visibility = visibility;
    }

    public static RoomJoinResult Joined(RoomVisibility visibility) => new(RoomJoinOutcome.Joined, visibility);

    public static RoomJoinResult Failed(RoomJoinOutcome outcome) => new(outcome, visibility: null);
}