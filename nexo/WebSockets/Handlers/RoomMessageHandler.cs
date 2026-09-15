using nexo.Rooms;
using nexo.Rooms.Models;
using nexo.WebSockets.Connection;
using nexo.WebSockets.Protocol;
using nexo.WebSockets.Protocol.Messages;

namespace nexo.WebSockets.Handlers;

/// <summary>
/// Translates validated protocol messages into room operations and the resulting protocol
/// responses. This is where transport (WebSocketConnection), protocol (parsing/envelopes), and
/// room state (RoomManager, Redis) come together — none of those layers know about each other
/// directly.
/// </summary>
public sealed class RoomMessageHandler(
    RoomManager roomManager,
    RoomConnectionRegistry connectionRegistry,
    ChatHistoryStore chatHistoryStore,
    ILogger<RoomMessageHandler> logger)
{
    public async Task HandleAsync(WebSocketConnection connection, ProtocolMessageType messageType, object payload, CancellationToken cancellationToken)
    {
        switch (messageType)
        {
            case ProtocolMessageType.JoinRoom:
                await HandleJoinRoomAsync(connection, (JoinRoomPayload)payload, cancellationToken);
                break;

            case ProtocolMessageType.LeaveRoom:
                await HandleLeaveRoomAsync(connection, (LeaveRoomPayload)payload, cancellationToken);
                break;

            case ProtocolMessageType.ChatMessage:
                await HandleChatMessageAsync(connection, (ChatMessagePayload)payload, cancellationToken);
                break;

            case ProtocolMessageType.ParticipantSpeakingStateChanged:
                HandleSpeakingStateChanged(connection, (ParticipantSpeakingStatePayload)payload);
                break;

            default:
                // The parser only ever produces client-permitted message types, so this should be
                // unreachable; logged defensively rather than silently ignored.
                logger.LogWarning("Connection {ConnectionId} produced an unhandled message type {MessageType}.", connection.ConnectionId, messageType);
                break;
        }
    }

    /// <summary>Releases room membership and broadcast registration when a connection closes.</summary>
    public async Task HandleDisconnectedAsync(WebSocketConnection connection)
    {
        var roomId = connection.CurrentRoomId;
        if (roomId is null)
        {
            return;
        }

        connectionRegistry.Unregister(roomId, connection);
        await roomManager.LeaveRoomAsync(roomId, connection.ParticipantId, CancellationToken.None);
    }

    private async Task HandleJoinRoomAsync(WebSocketConnection connection, JoinRoomPayload payload, CancellationToken cancellationToken)
    {
        if (connection.CurrentRoomId is not null && connection.CurrentRoomId != payload.RoomId)
        {
            SendError(connection, ProtocolErrorCode.AlreadyInRoom, "You must leave your current room before joining another.");
            return;
        }

        var result = await roomManager.JoinRoomAsync(payload.RoomId, connection.ParticipantId, payload.Password, cancellationToken);

        if (!result.IsSuccess)
        {
            var errorCode = result.Outcome switch
            {
                RoomJoinOutcome.InvalidPassword => ProtocolErrorCode.InvalidRoomPassword,
                RoomJoinOutcome.RoomFull => ProtocolErrorCode.RoomFull,
                RoomJoinOutcome.TemporarilyUnavailable => ProtocolErrorCode.RoomTemporarilyUnavailable,
                _ => ProtocolErrorCode.InvalidPayload
            };

            SendError(connection, errorCode, "Could not join the room.");
            return;
        }

        connection.CurrentRoomId = payload.RoomId;
        connectionRegistry.Register(payload.RoomId, connection);

        logger.LogInformation("Connection {ConnectionId} joined room {RoomId}.", connection.ConnectionId, payload.RoomId);

        connection.TryEnqueueSend(ProtocolEnvelopeWriter.Write(ProtocolMessageType.RoomJoined, new RoomJoinedPayload
        {
            RoomId = payload.RoomId,
            ParticipantId = connection.ParticipantId,
            Visibility = result.Visibility!.Value
        }));

        var history = await chatHistoryStore.GetHistoryAsync(payload.RoomId, cancellationToken);
        connection.TryEnqueueSend(ProtocolEnvelopeWriter.Write(ProtocolMessageType.RoomHistory, new RoomHistoryPayload
        {
            Messages = history
        }));
    }

    private async Task HandleLeaveRoomAsync(WebSocketConnection connection, LeaveRoomPayload payload, CancellationToken cancellationToken)
    {
        if (connection.CurrentRoomId != payload.RoomId)
        {
            SendError(connection, ProtocolErrorCode.NotInRoom, "You are not a member of this room.");
            return;
        }

        connectionRegistry.Unregister(payload.RoomId, connection);
        await roomManager.LeaveRoomAsync(payload.RoomId, connection.ParticipantId, cancellationToken);
        connection.CurrentRoomId = null;

        logger.LogInformation("Connection {ConnectionId} left room {RoomId}.", connection.ConnectionId, payload.RoomId);
    }

    private async Task HandleChatMessageAsync(WebSocketConnection connection, ChatMessagePayload payload, CancellationToken cancellationToken)
    {
        if (connection.CurrentRoomId != payload.RoomId)
        {
            SendError(connection, ProtocolErrorCode.NotInRoom, "You must join this room before sending messages to it.");
            return;
        }

        var entry = new ChatHistoryEntry
        {
            MessageId = payload.MessageId,
            SenderId = connection.ParticipantId,
            Ciphertext = payload.Ciphertext,
            Nonce = payload.Nonce,
            ReplyToMessageId = payload.ReplyToMessageId,
            SentAtUtc = DateTimeOffset.UtcNow
        };

        var isNewMessage = await chatHistoryStore.TryStoreAsync(payload.RoomId, entry, cancellationToken);

        // Always ack, even for a de-duplicated resend, so the client's pending-outbox is cleared
        // whether this is the original delivery or a retry after a previously dropped ack.
        connection.TryEnqueueSend(ProtocolEnvelopeWriter.Write(ProtocolMessageType.MessageAck, new MessageAckPayload
        {
            MessageId = payload.MessageId
        }));

        if (!isNewMessage)
        {
            return;
        }

        var relayBytes = ProtocolEnvelopeWriter.Write(ProtocolMessageType.ChatMessage, entry);

        foreach (var participantConnection in connectionRegistry.GetConnections(payload.RoomId))
        {
            if (participantConnection.ConnectionId == connection.ConnectionId)
            {
                continue;
            }

            participantConnection.TryEnqueueSend(relayBytes);
        }
    }

    private void HandleSpeakingStateChanged(WebSocketConnection connection, ParticipantSpeakingStatePayload payload)
    {
        if (connection.CurrentRoomId is null)
        {
            SendError(connection, ProtocolErrorCode.NotInRoom, "You must join a room before sending participant state.");
            return;
        }

        // The relayed identity is always the server's own record of this connection, never the
        // client-supplied ParticipantId, so a participant can never claim to be someone else.
        var outgoing = new ParticipantSpeakingStatePayload
        {
            ParticipantId = connection.ParticipantId,
            IsSpeaking = payload.IsSpeaking
        };

        var bytes = ProtocolEnvelopeWriter.Write(ProtocolMessageType.ParticipantSpeakingStateChanged, outgoing);

        foreach (var participantConnection in connectionRegistry.GetConnections(connection.CurrentRoomId))
        {
            if (participantConnection.ConnectionId == connection.ConnectionId)
            {
                continue;
            }

            participantConnection.TryEnqueueSend(bytes);
        }
    }

    private static void SendError(WebSocketConnection connection, ProtocolErrorCode code, string message) =>
        connection.TryEnqueueSend(ProtocolEnvelopeWriter.Write(ProtocolMessageType.ProtocolError, new ProtocolErrorPayload(code, message)));
}