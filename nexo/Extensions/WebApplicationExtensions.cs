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
    /// Maps the WebSocket upgrade endpoint. Each accepted connection is handed off to a
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
            ILoggerFactory loggerFactory) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            await using var connection = new WebSocketConnection(
                socket,
                parser,
                roomMessageHandler,
                settings,
                loggerFactory.CreateLogger<WebSocketConnection>());

            connection.OnClosedAsync = roomMessageHandler.HandleDisconnectedAsync;

            await connection.RunAsync(context.RequestAborted);
        });

        return app;
    }
}