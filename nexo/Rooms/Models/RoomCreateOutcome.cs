namespace nexo.Rooms.Models;

/// <summary>The result of attempting to create a room, for the caller to translate into a protocol response.</summary>
public enum RoomCreateOutcome
{
    Created,
    TemporarilyUnavailable,
}