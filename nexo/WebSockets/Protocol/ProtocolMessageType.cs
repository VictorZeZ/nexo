namespace nexo.WebSockets.Protocol;

/// <summary>
/// Identifies the kind of payload carried by a <see cref="ProtocolEnvelope"/>.
/// New message types must only ever be appended with a new explicit value; existing values
/// must never be renumbered or reused, since these numbers are part of the wire contract.
/// </summary>
public enum ProtocolMessageType
{
    Unknown = 0,

    // Room lifecycle (client -> server)
    JoinRoom = 1,
    LeaveRoom = 2,

    // Chat (bidirectional / server -> client, per member)
    ChatMessage = 10,
    MessageAck = 11,
    RoomHistory = 12,

    // Participant state (client -> server, relayed to others)
    ParticipantSpeakingStateChanged = 20,

    // Protocol-level (server -> client)
    ProtocolError = 900,
}