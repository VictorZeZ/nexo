using nexo.Options;
using nexo.WebSockets.Protocol;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace nexo.WebSockets.Connection;

/// <summary>
/// Owns the full lifecycle of a single WebSocket connection: receiving and reassembling
/// application messages, parsing them via <see cref="ProtocolMessageParser"/>, and serializing
/// all outbound sends through a single dedicated queue so concurrent sends never race on the
/// same socket. No room or business logic lives here (see later steps).
/// </summary>
public sealed class WebSocketConnection : IAsyncDisposable
{
    /// <summary>
    /// Identifies this connection only — never a permanent user identity. A reconnecting
    /// client is a brand new <see cref="WebSocketConnection"/> with a new <see cref="ConnectionId"/>.
    /// </summary>
    public Guid ConnectionId { get; } = Guid.NewGuid();

    private readonly WebSocket _socket;
    private readonly ProtocolMessageParser _parser;
    private readonly WebSocketConnectionSettings _settings;
    private readonly ILogger<WebSocketConnection> _logger;
    private readonly Channel<byte[]> _outbox;
    private readonly byte[] _receiveBuffer;
    private readonly CancellationTokenSource _connectionCts = new();

    public WebSocketConnection(
        WebSocket socket,
        ProtocolMessageParser parser,
        WebSocketConnectionSettings settings,
        ILogger<WebSocketConnection> logger)
    {
        _socket = socket;
        _parser = parser;
        _settings = settings;
        _logger = logger;

        _receiveBuffer = ArrayPool<byte>.Shared.Rent(settings.MaxMessageSizeBytes);

        _outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(settings.SendQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
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
    /// requested shutdown via <paramref name="hostShutdownToken"/>, or the connection was aborted
    /// (e.g. an oversized message or a full outbound queue).
    /// </summary>
    public async Task RunAsync(CancellationToken hostShutdownToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken, _connectionCts.Token);

        var receiveTask = ReceiveLoopAsync(linkedCts.Token);
        var sendTask = SendLoopAsync(linkedCts.Token);

        await receiveTask;

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

                // Text and binary frames are treated identically: the protocol is encoding-agnostic
                // (see ProtocolMessageParser), so the wire encoding can change later without touching this loop.
                HandleParsedMessage(_parser.Parse(new ReadOnlySpan<byte>(_receiveBuffer, 0, totalBytesReceived)));
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

    private void HandleParsedMessage(ProtocolParseResult parseResult)
    {
        if (parseResult.IsSuccess)
        {
            // No room or business logic exists yet — wired up in a later step. For now, correctly
            // receiving, reassembling, and validating a message is the full scope of this step.
            _logger.LogDebug("Connection {ConnectionId} sent a valid {MessageType} message.", ConnectionId, parseResult.MessageType);
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

    public ValueTask DisposeAsync()
    {
        ArrayPool<byte>.Shared.Return(_receiveBuffer);
        _connectionCts.Dispose();
        return ValueTask.CompletedTask;
    }
}