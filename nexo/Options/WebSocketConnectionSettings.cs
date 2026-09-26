namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "WebSocketConnection" configuration section.
/// Structural limits and transport settings for the WebSocket layer itself, independent of
/// protocol-level payload limits (see ProtocolLimitsSettings).
/// </summary>
public sealed class WebSocketConnectionSettings
{
    public const string SectionName = "WebSocketConnection";

    /// <summary>
    /// Maximum size, in bytes, of a single fully-assembled application message (after
    /// reassembling any fragmented WebSocket frames). Exceeding this closes the connection,
    /// preventing unbounded memory growth from a single oversized or malicious message.
    /// </summary>
    public int MaxMessageSizeBytes { get; init; } = 16 * 1024;

    /// <summary>
    /// Maximum number of outbound messages that may be queued for a single connection before
    /// it is considered too slow and is closed, so one slow client cannot consume unbounded
    /// memory or block whoever is trying to send to it.
    /// </summary>
    public int SendQueueCapacity { get; init; } = 64;

    /// <summary>
    /// Interval at which the underlying transport sends WebSocket-protocol ping frames to keep
    /// the connection alive. This is distinct from any future application-level heartbeat.
    /// </summary>
    public int KeepAliveIntervalSeconds { get; init; } = 30;

    /// <summary>Maximum number of concurrent WebSocket connections a single client IP address may hold open.</summary>
    public int MaxConcurrentConnectionsPerIp { get; init; } = 20;

    /// <summary>Token-bucket burst capacity for inbound application messages on a single connection.</summary>
    public int MessageBurstCapacity { get; init; } = 60;

    /// <summary>Sustained inbound messages per second a connection is allowed once its burst capacity is used up.</summary>
    public int MessagesPerSecond { get; init; } = 30;
}