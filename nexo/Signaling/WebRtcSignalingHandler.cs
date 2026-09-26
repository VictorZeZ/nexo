using nexo.Rooms;
using nexo.WebSockets.Connection;
using nexo.WebSockets.Protocol;
using nexo.WebSockets.Protocol.Messages;

namespace nexo.Signaling;

/// <summary>
/// Relays WebRTC signaling messages (SDP offers/answers, ICE candidates) between two
/// participants of the same room, so they can establish a direct peer-to-peer connection for
/// voice. The server never inspects, modifies, or stores the SDP/ICE payloads it relays, and
/// audio itself never passes through this server at all.
/// </summary>
public sealed class WebRtcSignalingHandler(RoomConnectionRegistry connectionRegistry)
{
    public void HandleOffer(WebSocketConnection connection, WebRtcOfferPayload payload) =>
        Relay(connection, payload.ToParticipantId, ProtocolMessageType.WebRtcOffer, payload with
        {
            FromParticipantId = connection.ParticipantId
        });

    public void HandleAnswer(WebSocketConnection connection, WebRtcAnswerPayload payload) =>
        Relay(connection, payload.ToParticipantId, ProtocolMessageType.WebRtcAnswer, payload with
        {
            FromParticipantId = connection.ParticipantId
        });

    public void HandleIceCandidate(WebSocketConnection connection, WebRtcIceCandidatePayload payload) =>
        Relay(connection, payload.ToParticipantId, ProtocolMessageType.WebRtcIceCandidate, payload with
        {
            FromParticipantId = connection.ParticipantId
        });

    private void Relay(WebSocketConnection sender, string toParticipantId, ProtocolMessageType type, object outgoingPayload)
    {
        if (sender.CurrentRoomId is null)
        {
            SendError(sender, ProtocolErrorCode.NotInRoom, "You must join a room before sending WebRTC signaling messages.");
            return;
        }

        if (toParticipantId == sender.ParticipantId)
        {
            SendError(sender, ProtocolErrorCode.InvalidPayload, "Cannot send a signaling message to yourself.");
            return;
        }

        var target = connectionRegistry.FindConnection(sender.CurrentRoomId, toParticipantId);
        if (target is null)
        {
            SendError(sender, ProtocolErrorCode.PeerNotFound, "The target participant is not currently connected to this room.");
            return;
        }

        target.TryEnqueueSend(ProtocolEnvelopeWriter.Write(type, outgoingPayload));
    }

    private static void SendError(WebSocketConnection connection, ProtocolErrorCode code, string message) =>
        connection.TryEnqueueSend(ProtocolEnvelopeWriter.Write(ProtocolMessageType.ProtocolError, new ProtocolErrorPayload(code, message)));
}