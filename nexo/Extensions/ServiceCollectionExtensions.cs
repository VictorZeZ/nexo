using nexo.Middleware;
using nexo.Options;
using System.Threading.RateLimiting;

namespace nexo.Extensions;

/// <summary>
/// Registers application services and cross-cutting concerns, keeping Program.cs minimal.
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "NexoCorsPolicy";

    /// <summary>
    /// Registers centralized exception handling that converts unhandled exceptions into ProblemDetails.
    /// </summary>
    public static IServiceCollection AddNexoProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Registers a CORS policy built from explicit, configured allowed origins.
    /// If no origins are configured, cross-origin requests are denied rather than falling back to a wildcard.
    /// </summary>
    public static IServiceCollection AddNexoCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (corsSettings.AllowedOrigins.Length == 0)
                {
                    return;
                }

                policy.WithOrigins(corsSettings.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// Registers a global, fixed-window rate limiter partitioned per client IP address.
    /// </summary>
    public static IServiceCollection AddNexoRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitingSettings = configuration.GetSection(RateLimitingSettings.SectionName).Get<RateLimitingSettings>()
            ?? new RateLimitingSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitingSettings.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitingSettings.WindowSeconds),
                    QueueLimit = rateLimitingSettings.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }
}