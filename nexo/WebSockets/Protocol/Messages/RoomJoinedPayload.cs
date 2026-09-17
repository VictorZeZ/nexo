using nexo.Rooms.Models;

namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by the server to confirm a client has successfully joined (or just created and joined) a room.</summary>
public sealed record RoomJoinedPayload
{
    public required string RoomId { get; init; }
    public required string RoomName { get; init; }
    public required string ParticipantId { get; init; }
    public required RoomVisibility Visibility { get; init; }
}