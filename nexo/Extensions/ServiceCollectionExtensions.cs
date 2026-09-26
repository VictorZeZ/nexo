using nexo.BackgroundServices;
using nexo.Middleware;
using nexo.Options;
using nexo.Rooms;
using nexo.WebSockets.Connection;
using nexo.WebSockets.Handlers;
using nexo.WebSockets.Protocol;
using StackExchange.Redis;
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
    /// Also registers CorsSettings itself so the WebSocket endpoint can reuse the same allow-list
    /// for Origin header validation on the handshake.
    /// </summary>
    public static IServiceCollection AddNexoCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        services.AddSingleton(corsSettings);

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

    /// <summary>
    /// Registers a long-lived Redis connection multiplexer. The multiplexer is thread-safe and
    /// designed to be shared for the lifetime of the app, so it is registered as a singleton.
    /// Connection failures do not crash startup: StackExchange.Redis retries in the background,
    /// and callers must be prepared to handle a temporarily unavailable connection.
    /// </summary>
    public static IServiceCollection AddNexoRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>()
            ?? new RedisSettings();

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var configurationOptions = ConfigurationOptions.Parse(redisSettings.ConnectionString);
            configurationOptions.AbortOnConnectFail = false;

            var logger = serviceProvider.GetRequiredService<ILogger<IConnectionMultiplexer>>();

            var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);

            multiplexer.ConnectionFailed += (_, args) =>
                logger.LogError(args.Exception, "Redis connection failed: {FailureType}", args.FailureType);

            multiplexer.ConnectionRestored += (_, _) =>
                logger.LogInformation("Redis connection restored.");

            return multiplexer;
        });

        return services;
    }

    /// <summary>
    /// Registers the WebSocket protocol parser and its configured structural size limits.
    /// </summary>
    public static IServiceCollection AddNexoWebSocketProtocol(this IServiceCollection services, IConfiguration configuration)
    {
        var protocolLimits = configuration.GetSection(ProtocolLimitsSettings.SectionName).Get<ProtocolLimitsSettings>()
            ?? new ProtocolLimitsSettings();

        services.AddSingleton(protocolLimits);
        services.AddSingleton<ProtocolMessageParser>();

        return services;
    }

    /// <summary>
    /// Registers structural limits and transport settings for the WebSocket connection layer,
    /// plus the per-IP concurrent connection limiter.
    /// </summary>
    public static IServiceCollection AddNexoWebSocketConnections(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(WebSocketConnectionSettings.SectionName).Get<WebSocketConnectionSettings>()
            ?? new WebSocketConnectionSettings();

        services.AddSingleton(settings);
        services.AddSingleton<IpConnectionLimiter>();

        return services;
    }

    /// <summary>
    /// Registers room management: server-side room state, membership, capacity, private-room
    /// password verification, chat history storage, live connection tracking, reconnect grace
    /// tracking, and the message handler that ties rooms to the WebSocket layer.
    /// </summary>
    public static IServiceCollection AddNexoRooms(this IServiceCollection services, IConfiguration configuration)
    {
        var roomSettings = configuration.GetSection(RoomSettings.SectionName).Get<RoomSettings>()
            ?? new RoomSettings();

        services.AddSingleton(roomSettings);
        services.AddSingleton<RoomManager>();
        services.AddSingleton<RoomConnectionRegistry>();
        services.AddSingleton<ChatHistoryStore>();
        services.AddSingleton<PendingRoomRemovalTracker>();
        services.AddSingleton<RoomMessageHandler>();

        return services;
    }

    /// <summary>
    /// Registers application-level connection health monitoring: a background sweep that
    /// heartbeats live connections and closes any that have gone silent for too long.
    /// </summary>
    public static IServiceCollection AddNexoHeartbeat(this IServiceCollection services, IConfiguration configuration)
    {
        var heartbeatSettings = configuration.GetSection(HeartbeatSettings.SectionName).Get<HeartbeatSettings>()
            ?? new HeartbeatSettings();

        services.AddSingleton(heartbeatSettings);
        services.AddHostedService<StaleConnectionSweepService>();

        return services;
    }
}