namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by the server to the original sender once a chat message has been stored and relayed,
/// so the client can clear it from its local pending-resend outbox.
/// </summary>
public sealed record MessageAckPayload
{
    public required Guid MessageId { get; init; }
}