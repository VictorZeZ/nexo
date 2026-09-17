namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by the server to confirm a client's display name has been recorded.</summary>
public sealed record IdentityConfirmedPayload
{
    public required string DisplayName { get; init; }
}