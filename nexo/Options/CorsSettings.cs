namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "Cors" configuration section.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Origins allowed to make cross-origin requests to the API.
    /// Must be explicit, fully qualified origins (scheme + host + port). No wildcards.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}