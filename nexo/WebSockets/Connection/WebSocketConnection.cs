using nexo.Options;
using nexo.WebSockets.Handlers;
using nexo.WebSockets.Protocol;
using nexo.WebSockets.Protocol.Messages;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;
using System.Threading.RateLimiting;

namespace nexo.WebSockets.Connection;

/// <summary>
/// Owns the full lifecycle of a single WebSocket connection: receiving and reassembling
/// application messages, parsing them via <see cref="ProtocolMessageParser"/>, dispatching valid
/// messages to <see cref="RoomMessageHandler"/>, and serializing all outbound sends through a
/// single dedicated queue so concurrent sends never race on the same socket. Inbound messages are
/// rate-limited per connection to prevent a single client from flooding the server.
/// </summary>
public sealed class WebSocketConnection : IAsyncDisposable
{
    /// <summary>
    /// Identifies this connection only — never a permanent user identity. A reconnecting
    /// client is a brand new <see cref="WebSocketConnection"/> with a new <see cref="ConnectionId"/>.
    /// </summary>
    public Guid ConnectionId { get; } = Guid.NewGuid();

    /// <summary>
    /// The room-scoped identity used for membership, ownership, and relay attribution. Defaults
    /// to this connection's own ID, but is overwritten when a client successfully resumes a
    /// previous session via a valid reconnect token, restoring their original participant
    /// identity onto this new physical connection (see RoomMessageHandler).
    /// </summary>
    public string ParticipantId { get; set; }

    /// <summary>The room this connection currently belongs to, if any. Owned by the handler layer.</summary>
    public string? CurrentRoomId { get; set; }

    /// <summary>
    /// The display name established via SetDisplayName, if any. Purely cosmetic metadata —
    /// never used for authorization. Required before a room can be created or joined.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>UTC time of the last message received from the client, of any kind. Used to detect a stale connection.</summary>
    public DateTimeOffset LastActivityAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Sends a heartbeat message. Failure to enqueue (queue full) is handled the same as any other send.</summary>
    public void SendHeartbeat() =>
        TryEnqueueSend(ProtocolEnvelopeWriter.Write(ProtocolMessageType.Heartbeat, new HeartbeatPayload
        {
            SentAtUtc = DateTimeOffset.UtcNow
        }));

    /// <summary>Closes this connection because it has gone silent for too long.</summary>
    public void CloseAsStale() => _connectionCts.Cancel();

    /// <summary>Attempts to record a room creation for this connection, enforcing a per-connection cap.</summary>
    public bool TryRecordRoomCreated(int maxAllowed) => Interlocked.Increment(ref _roomsCreatedCount) <= maxAllowed;

    /// <summary>
    /// Optional cleanup invoked once the connection has fully stopped. Used by higher layers
    /// (e.g. room membership) to release connection-specific state without coupling this class
    /// to room or business logic.
    /// </summary>
    public Func<WebSocketConnection, Task>? OnClosedAsync { get; set; }

    private readonly WebSocket _socket;
    private readonly ProtocolMessageParser _parser;
    private readonly RoomMessageHandler _roomMessageHandler;
    private readonly WebSocketConnectionSettings _settings;
    private readonly ILogger<WebSocketConnection> _logger;
    private readonly Channel<byte[]> _outbox;
    private readonly byte[] _receiveBuffer;
    private readonly CancellationTokenSource _connectionCts = new();
    private readonly TokenBucketRateLimiter _messageRateLimiter;
    private int _roomsCreatedCount;

    public WebSocketConnection(
        WebSocket socket,
        ProtocolMessageParser parser,
        RoomMessageHandler roomMessageHandler,
        WebSocketConnectionSettings settings,
        ILogger<WebSocketConnection> logger)
    {
        _socket = socket;
        _parser = parser;
        _roomMessageHandler = roomMessageHandler;
        _settings = settings;
        _logger = logger;
        ParticipantId = ConnectionId.ToString();

        _receiveBuffer = ArrayPool<byte>.Shared.Rent(settings.MaxMessageSizeBytes);

        _outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(settings.SendQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _messageRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = settings.MessageBurstCapacity,
            TokensPerPeriod = settings.MessagesPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    /// <summary>
    /// Queues a message for sending. Returns false if the connection's outbound queue is full,
    /// meaning the client is too slow to keep up; the connection is then closed so a single slow
    /// client cannot consume unbounded memory or block whoever tried to send to it.
    /// </summary>
    public bool TryEnqueueSend(byte[] message)
    {
        if (_outbox.Writer.TryWrite(message))
        {
            return true;
        }

        _logger.LogWarning(
            "Connection {ConnectionId} outbound queue is full; closing as too slow to keep up.",
            ConnectionId);
        _connectionCts.Cancel();
        return false;
    }

    /// <summary>
    /// Runs the connection until it closes, either because the client disconnected, the server
    /// requested shutdown via <paramref name="hostShutdownToken"/>, or the connection was aborted.
    /// </summary>
    public async Task RunAsync(CancellationToken hostShutdownToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken, _connectionCts.Token);

        var receiveTask = ReceiveLoopAsync(linkedCts.Token);
        var sendTask = SendLoopAsync(linkedCts.Token);

        await receiveTask;

        if (OnClosedAsync is not null)
        {
            try
            {
                await OnClosedAsync(this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while cleaning up connection {ConnectionId}.", ConnectionId);
            }
        }

        _outbox.Writer.TryComplete();
        linkedCts.Cancel();

        await sendTask;

        await CloseSocketGracefullyAsync();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var totalBytesReceived = 0;
                WebSocketReceiveResult result;

                do
                {
                    if (totalBytesReceived >= _receiveBuffer.Length)
                    {
                        _logger.LogWarning(
                            "Connection {ConnectionId} sent a message exceeding the {MaxBytes} byte limit; closing.",
                            ConnectionId, _settings.MaxMessageSizeBytes);
                        await _socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large.", CancellationToken.None);
                        return;
                    }

                    var segment = new ArraySegment<byte>(_receiveBuffer, totalBytesReceived, _receiveBuffer.Length - totalBytesReceived);
                    result = await _socket.ReceiveAsync(segment, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        return;
                    }

                    totalBytesReceived += result.Count;
                }
                while (!result.EndOfMessage);

                using var rateLimitLease = _messageRateLimiter.AttemptAcquire(1);
                if (!rateLimitLease.IsAcquired)
                {
                    _logger.LogWarning("Connection {ConnectionId} exceeded the inbound message rate limit; closing.", ConnectionId);
                    await _socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Message rate limit exceeded.", CancellationToken.None);
                    return;
                }

                // Text and binary frames are treated identically: the protocol is encoding-agnostic
                // (see ProtocolMessageParser), so the wire encoding can change later without touching this loop.
                var parseResult = _parser.Parse(new ReadOnlySpan<byte>(_receiveBuffer, 0, totalBytesReceived));
                await HandleParsedMessageAsync(parseResult, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown or connection abort; not an application error.
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Connection {ConnectionId} closed abnormally during receive.", ConnectionId);
        }
    }

    private async Task HandleParsedMessageAsync(ProtocolParseResult parseResult, CancellationToken cancellationToken)
    {
        LastActivityAtUtc = DateTimeOffset.UtcNow;

        if (parseResult.IsSuccess)
        {
            try
            {
                await _roomMessageHandler.HandleAsync(this, parseResult.MessageType!.Value, parseResult.Payload!, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception while handling a {MessageType} message on connection {ConnectionId}.",
                    parseResult.MessageType, ConnectionId);
            }

            return;
        }

        // ProtocolParseResult guarantees Error is set whenever IsSuccess is false.
        var errorBytes = ProtocolEnvelopeWriter.Write(ProtocolMessageType.ProtocolError, parseResult.Error!);
        TryEnqueueSend(errorBytes);
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _outbox.Reader.ReadAllAsync(cancellationToken))
            {
                if (_socket.State != WebSocketState.Open)
                {
                    break;
                }

                await _socket.SendAsync(message, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown or connection abort; not an application error.
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Connection {ConnectionId} closed abnormally during send.", ConnectionId);
        }
    }

    private async Task CloseSocketGracefullyAsync()
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch (WebSocketException ex)
            {
                _logger.LogDebug(ex, "Connection {ConnectionId} could not be closed gracefully.", ConnectionId);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        ArrayPool<byte>.Shared.Return(_receiveBuffer);
        _connectionCts.Dispose();
        await _messageRateLimiter.DisposeAsync();
    }
}