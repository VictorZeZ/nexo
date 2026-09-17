namespace nexo.WebSockets.Protocol.Messages;

/// <summary>
/// Sent by a client to establish its display name for this connection, before creating or
/// joining any room. Purely cosmetic metadata — never used for authorization decisions.
/// </summary>
public sealed record SetDisplayNamePayload
{
    public required string DisplayName { get; init; }
}