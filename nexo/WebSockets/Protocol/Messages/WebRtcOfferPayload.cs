namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by a client to deliver a WebRTC SDP offer to a specific peer in the same room, as part
/// of establishing a direct (mesh) peer connection for voice. The server only relays this — it
/// never inspects, stores, or modifies the SDP itself.
/// </summary>
public sealed record WebRtcOfferPayload
{
    public required string ToParticipantId { get; init; }
    public required string Sdp { get; init; }

    /// <summary>Ignored on incoming messages; set by the server to the sender's real identity before relaying.</summary>
    public string? FromParticipantId { get; init; }
}