namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by the server to remaining room members when a participant has left — either
/// explicitly or after their reconnect grace period expired without a resume. Lets clients
/// tear down that participant's WebRTC peer connection and remove them from the UI.
/// </summary>
public sealed record ParticipantLeftPayload
{
    public required string ParticipantId { get; init; }
}