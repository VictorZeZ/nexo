namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by a client to join a room. Full membership/authorization logic lives in Step 5.</summary>
public sealed record JoinRoomPayload
{
    public required string RoomId { get; init; }

    /// <summary>Required only for private rooms; ignored for public ones.</summary>
    public string? Password { get; init; }
}