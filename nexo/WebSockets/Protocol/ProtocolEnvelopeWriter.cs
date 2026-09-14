using System.Text.Json;

namespace nexo.WebSockets.Protocol;

/// <summary>
/// Serializes outgoing protocol messages into the wire envelope format.
/// Returns raw UTF-8 bytes directly, avoiding an intermediate string allocation and keeping
/// the wire encoding (currently JSON) swappable without touching any caller.
/// </summary>
public static class ProtocolEnvelopeWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Write(ProtocolMessageType type, object payload)
    {
        var envelope = new
        {
            v = ProtocolVersion.Current,
            type,
            payload
        };

        return JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
    }
}