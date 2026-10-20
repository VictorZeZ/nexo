namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Identifies one participant who is currently connected to a room. Used both as an entry in the
/// roster delivered with <see cref="RoomJoinedPayload"/> and as the payload of a ParticipantJoined
/// message announced to existing members.
/// </summary>
public sealed record ParticipantInfo
{
    public required string ParticipantId { get; init; }

    /// <summary>Cosmetic only — never used for authorization.</summary>
    public required string DisplayName { get; init; }
}