using nexo.Options;
using nexo.WebSockets.Protocol.Messages;
using System.Text.Json;

namespace nexo.WebSockets.Protocol;

/// <summary>
/// Parses raw incoming WebSocket bytes into a validated protocol message.
/// Fully isolated from transport and business logic: given the same input it always produces
/// the same result, and it never touches sockets, rooms, or Redis.
/// Works directly on UTF-8 bytes so the wire encoding (currently JSON) can be swapped later
/// without changing any caller or any message type.
/// </summary>
public sealed class ProtocolMessageParser(ProtocolLimitsSettings limits)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public ProtocolParseResult Parse(ReadOnlySpan<byte> rawMessage)
    {
        ProtocolEnvelope envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<ProtocolEnvelope>(rawMessage, SerializerOptions)
                ?? throw new JsonException("Envelope deserialized to null.");
        }
        catch (JsonException)
        {
            return ProtocolParseResult.Failure(
                ProtocolErrorCode.MalformedEnvelope,
                "The message could not be parsed as a valid protocol envelope.");
        }

        if (envelope.Version < ProtocolVersion.MinimumSupported)
        {
            return ProtocolParseResult.Failure(
                ProtocolErrorCode.UnsupportedProtocolVersion,
                $"Protocol version {envelope.Version} is no longer supported.");
        }

        try
        {
            return envelope.Type switch
            {
                ProtocolMessageType.JoinRoom => ParsePayload<JoinRoomPayload>(envelope, ValidateJoinRoom),
                ProtocolMessageType.LeaveRoom => ParsePayload<LeaveRoomPayload>(envelope, ValidateLeaveRoom),
                ProtocolMessageType.ChatMessage => ParsePayload<ChatMessagePayload>(envelope, ValidateChatMessage),
                ProtocolMessageType.ParticipantSpeakingStateChanged =>
                    ParsePayload<ParticipantSpeakingStatePayload>(envelope, ValidateSpeakingState),
                _ => ProtocolParseResult.Failure(
                    ProtocolErrorCode.UnknownMessageType,
                    $"Message type '{envelope.Type}' is not recognized or cannot be sent by a client.")
            };
        }
        catch (JsonException)
        {
            return ProtocolParseResult.Failure(
                ProtocolErrorCode.InvalidPayload,
                $"The payload for message type '{envelope.Type}' is invalid.");
        }
    }

    private ProtocolParseResult ParsePayload<TPayload>(ProtocolEnvelope envelope, Func<TPayload, string?> validate) where TPayload : class
    {
        var payload = envelope.Payload.Deserialize<TPayload>(SerializerOptions)
            ?? throw new JsonException($"Payload deserialized to null for type {typeof(TPayload).Name}.");

        var validationError = validate(payload);

        return validationError is null
            ? ProtocolParseResult.Success(envelope.Type, payload)
            : ProtocolParseResult.Failure(ProtocolErrorCode.InvalidPayload, validationError);
    }

    private string? ValidateJoinRoom(JoinRoomPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.RoomId) || payload.RoomId.Length > limits.MaxRoomIdLength)
        {
            return "RoomId is required and must not exceed the maximum allowed length.";
        }

        if (payload.Password is { Length: > 0 } password && password.Length > limits.MaxPasswordLength)
        {
            return "Password exceeds the maximum allowed length.";
        }

        return null;
    }

    private string? ValidateLeaveRoom(LeaveRoomPayload payload) =>
        string.IsNullOrWhiteSpace(payload.RoomId) || payload.RoomId.Length > limits.MaxRoomIdLength
            ? "RoomId is required and must not exceed the maximum allowed length."
            : null;

    private string? ValidateChatMessage(ChatMessagePayload payload)
    {
        if (payload.MessageId == Guid.Empty)
        {
            return "MessageId is required.";
        }

        if (string.IsNullOrWhiteSpace(payload.RoomId) || payload.RoomId.Length > limits.MaxRoomIdLength)
        {
            return "RoomId is required and must not exceed the maximum allowed length.";
        }

        if (string.IsNullOrEmpty(payload.Ciphertext) || payload.Ciphertext.Length > limits.MaxCiphertextLength)
        {
            return "Ciphertext is required and must not exceed the maximum allowed length.";
        }

        if (string.IsNullOrEmpty(payload.Nonce) || payload.Nonce.Length > limits.MaxNonceLength)
        {
            return "Nonce is required and must not exceed the maximum allowed length.";
        }

        return null;
    }

    private string? ValidateSpeakingState(ParticipantSpeakingStatePayload payload) =>
        string.IsNullOrWhiteSpace(payload.ParticipantId) || payload.ParticipantId.Length > limits.MaxParticipantIdLength
            ? "ParticipantId is required and must not exceed the maximum allowed length."
            : null;
}