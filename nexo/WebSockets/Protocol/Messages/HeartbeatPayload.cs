namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by the server on an interval so clients (and the server itself) can detect a dead connection.</summary>
public sealed record HeartbeatPayload
{
    public required DateTimeOffset SentAtUtc { get; init; }
}