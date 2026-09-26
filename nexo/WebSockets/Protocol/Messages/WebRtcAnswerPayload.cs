namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by a client to deliver a WebRTC SDP answer to a specific peer, replying to an offer.</summary>
public sealed record WebRtcAnswerPayload
{
    public required string ToParticipantId { get; init; }
    public required string Sdp { get; init; }

    /// <summary>Ignored on incoming messages; set by the server to the sender's real identity before relaying.</summary>
    public string? FromParticipantId { get; init; }
}