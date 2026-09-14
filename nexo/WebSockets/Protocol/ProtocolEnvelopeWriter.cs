using System.Text.Json;

namespace nexo.WebSockets.Protocol;

/// <summary>
/// Serializes outgoing protocol messages into the wire envelope format.
/// </summary>
public static class ProtocolEnvelopeWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Write(ProtocolMessageType type, object payload)
    {
        var envelope = new
        {
            v = ProtocolVersion.Current,
            type,
            payload
        };

        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }
}