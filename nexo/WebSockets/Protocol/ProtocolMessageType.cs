namespace nexo.WebSockets.Protocol;

/// <summary>
/// Identifies the kind of payload carried by a <see cref="ProtocolEnvelope"/>.
/// New message types must only ever be appended with a new explicit value; existing values
/// must never be renumbered or reused, since these numbers are part of the wire contract.
/// </summary>
public enum ProtocolMessageType
{
    Unknown = 0,

    // Room lifecycle (client -> server, and RoomJoined server -> client)
    JoinRoom = 1,
    LeaveRoom = 2,
    RoomJoined = 3,
    CreateRoom = 4,

    // Chat (bidirectional: ChatMessagePayload from client, ChatHistoryEntry when relayed/server -> client)
    ChatMessage = 10,
    MessageAck = 11,
    RoomHistory = 12,

    // Participant state (client -> server, relayed to others)
    ParticipantSpeakingStateChanged = 20,

    // Identity (client -> server, and confirmation server -> client)
    SetDisplayName = 30,
    IdentityConfirmed = 31,

    // Protocol-level (server -> client)
    ProtocolError = 900,

    // Connection health (server -> client)
    Heartbeat = 40,
}