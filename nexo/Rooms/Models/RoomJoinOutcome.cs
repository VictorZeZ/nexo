namespace nexo.Rooms.Models;

/// <summary>The result of attempting to join a room, for the caller to translate into a protocol response.</summary>
public enum RoomJoinOutcome
{
    Joined,
    RoomNotFound,
    InvalidPassword,
    RoomFull,
    TemporarilyUnavailable,
}