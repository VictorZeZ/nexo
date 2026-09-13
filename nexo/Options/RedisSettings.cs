namespace nexo.Options;

/// <summary>
/// Strongly typed binding for the "Redis" configuration section.
/// </summary>
public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string (e.g. "localhost:6379"). In production, supply this via
    /// an environment variable or user secrets — never commit a real connection string.
    /// </summary>
    public string ConnectionString { get; init; } = "localhost:6379";
}