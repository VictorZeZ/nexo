namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "RateLimiting" configuration section.
/// Applies a fixed-window limiter, partitioned per client IP address, to all HTTP requests.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Maximum number of requests allowed per client, per window.
    /// </summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>
    /// Length of the fixed window, in seconds.
    /// </summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Requests queued once the permit limit is reached, before being rejected outright.
    /// </summary>
    public int QueueLimit { get; init; } = 0;
}