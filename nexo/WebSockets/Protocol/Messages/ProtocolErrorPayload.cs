namespace nexo.WebSockets.Protocol.Messages;

/// <summary>Sent to a client when one of its messages fails parsing or validation.</summary>
public sealed record ProtocolErrorPayload(ProtocolErrorCode Code, string Message);