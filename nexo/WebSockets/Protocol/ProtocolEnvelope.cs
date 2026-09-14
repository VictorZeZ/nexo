using System.Text.Json;
using System.Text.Json.Serialization;

namespace nexo.WebSockets.Protocol;

/// <summary>
/// The outer wire format for every WebSocket message exchanged with the server.
/// The payload is kept as raw JSON and only deserialized into a concrete type after
/// the message type and protocol version have been validated.
/// </summary>
public sealed class ProtocolEnvelope
{
    [JsonPropertyName("v")]
    public int Version { get; init; }

    [JsonPropertyName("type")]
    public ProtocolMessageType Type { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; init; }
}