namespace nexo.WebSockets.Protocol;

/// <summary>
/// Machine-readable protocol error categories. Only enough information for a client to react
/// correctly — never internal exception details (rule: never expose internal server details).
/// </summary>
public enum ProtocolErrorCode
{
    UnknownMessageType = 1,
    UnsupportedProtocolVersion = 2,
    MalformedEnvelope = 3,
    InvalidPayload = 4,
}