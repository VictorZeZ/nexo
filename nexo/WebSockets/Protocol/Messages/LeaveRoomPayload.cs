namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by a client to explicitly leave a room.</summary>
public sealed record LeaveRoomPayload
{
    public required string RoomId { get; init; }
}