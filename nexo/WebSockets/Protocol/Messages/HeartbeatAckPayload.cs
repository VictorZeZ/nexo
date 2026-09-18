namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent by a client in reply to a Heartbeat, proving the connection is still alive.</summary>
public sealed record HeartbeatAckPayload
{
    /// <summary>Echoes the SentAtUtc from the Heartbeat being acknowledged.</summary>
    public required DateTimeOffset SentAtUtc { get; init; }
}