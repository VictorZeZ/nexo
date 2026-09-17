namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by a client when their local voice-activity detection transitions, and relayed by the
/// server to other room participants so their UI can reflect who is currently speaking.
/// </summary>
public sealed record ParticipantSpeakingStatePayload
{
    public required string ParticipantId { get; init; }
    public required bool IsSpeaking { get; init; }

    /// <summary>Ignored on incoming messages; set by the server to its own record before relaying.</summary>
    public string? DisplayName { get; init; }
}