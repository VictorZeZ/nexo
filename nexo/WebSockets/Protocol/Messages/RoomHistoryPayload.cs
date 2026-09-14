namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by the server when a client connects to a room, containing the recently retained chat
/// history. Entries remain end-to-end encrypted; the server only ever stores and forwards ciphertext.
/// </summary>
public sealed record RoomHistoryPayload
{
    public required IReadOnlyList<ChatHistoryEntry> Messages { get; init; }
}

public sealed record ChatHistoryEntry
{
    public required Guid MessageId { get; init; }
    public required string SenderId { get; init; }
    public required string Ciphertext { get; init; }
    public required string Nonce { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public required DateTimeOffset SentAtUtc { get; init; }
}