namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by a client reconnecting after a dropped connection, presenting a previously issued
/// reconnect token to resume its exact prior participant identity and room membership.
/// </summary>
public sealed record ResumeSessionPayload
{
    public required string ReconnectToken { get; init; }
}