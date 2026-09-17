namespace nexo.Rooms.Models;

/// <summary>
/// The outcome of a room creation attempt. Exactly one of the successful fields
/// (<see cref="RoomId"/>/<see cref="RoomName"/>/<see cref="Visibility"/>) or a failure
/// <see cref="Outcome"/> other than <see cref="RoomCreateOutcome.Created"/> is meaningful.
/// </summary>
public sealed class RoomCreateResult
{
    public RoomCreateOutcome Outcome { get; }
    public string? RoomId { get; }
    public string? RoomName { get; }
    public RoomVisibility? Visibility { get; }

    public bool IsSuccess => Outcome == RoomCreateOutcome.Created;

    private RoomCreateResult(RoomCreateOutcome outcome, string? roomId, string? roomName, RoomVisibility? visibility)
    {
        Outcome = outcome;
        RoomId = roomId;
        RoomName = roomName;
        Visibility = visibility;
    }

    public static RoomCreateResult Created(string roomId, string roomName, RoomVisibility visibility) =>
        new(RoomCreateOutcome.Created, roomId, roomName, visibility);

    public static RoomCreateResult Failed(RoomCreateOutcome outcome) => new(outcome, roomId: null, roomName: null, visibility: null);
}