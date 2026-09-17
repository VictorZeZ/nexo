namespace nexo.Rooms.Models;

/// <summary>
/// The outcome of a join attempt. Exactly one of successful <see cref="Visibility"/>/<see cref="RoomName"/>
/// or a failure <see cref="Outcome"/> other than <see cref="RoomJoinOutcome.Joined"/> is meaningful.
/// </summary>
public sealed class RoomJoinResult
{
    public RoomJoinOutcome Outcome { get; }
    public RoomVisibility? Visibility { get; }
    public string? RoomName { get; }

    public bool IsSuccess => Outcome == RoomJoinOutcome.Joined;

    private RoomJoinResult(RoomJoinOutcome outcome, RoomVisibility? visibility, string? roomName)
    {
        Outcome = outcome;
        Visibility = visibility;
        RoomName = roomName;
    }

    public static RoomJoinResult Joined(RoomVisibility visibility, string roomName) =>
        new(RoomJoinOutcome.Joined, visibility, roomName);

    public static RoomJoinResult Failed(RoomJoinOutcome outcome) => new(outcome, visibility: null, roomName: null);
}