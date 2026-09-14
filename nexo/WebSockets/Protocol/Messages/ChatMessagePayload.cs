namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by a client to deliver an end-to-end encrypted chat message to a room.
/// The server never sees plaintext: <see cref="Ciphertext"/> and <see cref="Nonce"/> are opaque,
/// encrypted client-side with a room key the server never possesses.
/// </summary>
public sealed record ChatMessagePayload
{
    /// <summary>
    /// Client-generated unique ID. Used by the server to de-duplicate a resend after a dropped
    /// acknowledgement, and referenced by later replies via <see cref="ReplyToMessageId"/>.
    /// </summary>
    public required Guid MessageId { get; init; }

    public required string RoomId { get; init; }

    /// <summary>Base64-encoded AES-GCM ciphertext.</summary>
    public required string Ciphertext { get; init; }

    /// <summary>Base64-encoded AES-GCM nonce used to produce <see cref="Ciphertext"/>.</summary>
    public required string Nonce { get; init; }

    /// <summary>Optional ID of the message this one replies to.</summary>
    public Guid? ReplyToMessageId { get; init; }
}