namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by a client to deliver one ICE candidate to a specific peer (trickle ICE).</summary>
public sealed record WebRtcIceCandidatePayload
{
    public required string ToParticipantId { get; init; }
    public required string Candidate { get; init; }
    public string? SdpMid { get; init; }
    public int? SdpMLineIndex { get; init; }

    /// <summary>Ignored on incoming messages; set by the server to the sender's real identity before relaying.</summary>
    public string? FromParticipantId { get; init; }
}