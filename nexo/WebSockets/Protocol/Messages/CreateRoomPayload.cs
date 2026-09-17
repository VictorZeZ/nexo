using nexo.Rooms.Models;

namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by a client to create a new room. The server generates and returns the room's join
/// code (see <see cref="RoomJoinedPayload"/>); this payload only supplies the display name and
/// access mode chosen by the creator.
/// </summary>
public sealed record CreateRoomPayload
{
    public required string RoomName { get; init; }
    public required RoomVisibility Visibility { get; init; }

    /// <summary>Required when <see cref="Visibility"/> is Private; must be omitted when Public.</summary>
    public string? Password { get; init; }
}