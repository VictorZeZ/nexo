using nexo.WebSockets.Protocol.Messages;

namespace nexo.WebSockets.Protocol;

/// <summary>
/// The outcome of attempting to parse and validate an incoming protocol message.
/// Exactly one of (<see cref="MessageType"/> + <see cref="Payload"/>) or <see cref="Error"/> is set.
/// </summary>
public sealed class ProtocolParseResult
{
    public ProtocolMessageType? MessageType { get; }
    public object? Payload { get; }
    public ProtocolErrorPayload? Error { get; }

    public bool IsSuccess => Error is null;

    private ProtocolParseResult(ProtocolMessageType? messageType, object? payload, ProtocolErrorPayload? error)
    {
        MessageType = messageType;
        Payload = payload;
        Error = error;
    }

    public static ProtocolParseResult Success(ProtocolMessageType messageType, object payload) =>
        new(messageType, payload, error: null);

    public static ProtocolParseResult Failure(ProtocolErrorCode code, string message) =>
        new(messageType: null, payload: null, new ProtocolErrorPayload(code, message));
}