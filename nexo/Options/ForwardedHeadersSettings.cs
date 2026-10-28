namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "ForwardedHeaders" configuration section.
/// Controls which reverse proxies are trusted to supply the original client IP address and
/// request scheme. When nothing is configured, X-Forwarded-* headers are ignored entirely, since
/// accepting them from an untrusted source would let a client spoof its IP and bypass rate limits.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Exact IP addresses of trusted proxies (e.g. "10.0.0.5"). Include loopback if the proxy runs on the same host.</summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>Trusted proxy networks in CIDR notation (e.g. "10.0.0.0/8").</summary>
    public string[] KnownNetworks { get; init; } = [];

    /// <summary>
    /// How many proxies in a chain to read forwarded addresses through. Use 1 for a single proxy
    /// and 2 for a chain such as CDN then load balancer.
    /// </summary>
    public int ForwardLimit { get; init; } = 1;

    /// <summary>True when at least one trusted proxy or network is configured.</summary>
    public bool IsConfigured => KnownProxies.Length > 0 || KnownNetworks.Length > 0;
}