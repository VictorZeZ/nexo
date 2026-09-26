using nexo.Options;
using nexo.WebSockets.Connection;
using nexo.WebSockets.Handlers;
using nexo.WebSockets.Protocol;

namespace nexo.Extensions;

/// <summary>
/// Wires up cross-cutting middleware and top-level endpoints, keeping Program.cs minimal.
/// </summary>
public static class WebApplicationExtensions
{
    public static WebApplication UseNexoRequestPipeline(this WebApplication app)
    {
        // Must run first so it can catch exceptions from everything downstream.
        app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        var webSocketSettings = app.Services.GetRequiredService<WebSocketConnectionSettings>();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(webSocketSettings.KeepAliveIntervalSeconds)
        });

        app.UseCors(ServiceCollectionExtensions.CorsPolicyName);

        app.UseRateLimiter();

        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// Maps the WebSocket upgrade endpoint. Validates the Origin header (when present) against
    /// the configured CORS allow-list and enforces a per-IP concurrent connection cap before
    /// accepting the socket. Each accepted connection is handed off to a
    /// <see cref="WebSocketConnection"/>, wired to <see cref="RoomMessageHandler"/> for both
    /// message dispatch and cleanup on disconnect.
    /// </summary>
    public static WebApplication MapNexoWebSocketEndpoint(this WebApplication app)
    {
        app.MapGet("/ws", async (
            HttpContext context,
            ProtocolMessageParser parser,
            RoomMessageHandler roomMessageHandler,
            WebSocketConnectionSettings settings,
            CorsSettings corsSettings,
            IpConnectionLimiter connectionLimiter,
            ILoggerFactory loggerFactory) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // A browser-sent Origin header must match the allow-list (defends against cross-site
            // WebSocket hijacking, since browsers do not apply same-origin policy to WebSockets).
            // A missing Origin header (native apps, server-to-server, tools like Postman) is allowed.
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) &&
                !corsSettings.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var remoteIp = context.Connection.RemoteIpAddress;
            if (!connectionLimiter.TryAcquire(remoteIp, settings.MaxConcurrentConnectionsPerIp))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();

                await using var connection = new WebSocketConnection(
                    socket,
                    parser,
                    roomMessageHandler,
                    settings,
                    loggerFactory.CreateLogger<WebSocketConnection>());

                connection.OnClosedAsync = roomMessageHandler.HandleDisconnectedAsync;

                await connection.RunAsync(context.RequestAborted);
            }
            finally
            {
                connectionLimiter.Release(remoteIp);
            }
        });

        return app;
    }
}