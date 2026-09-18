namespace nexo.Rooms;

/// <summary>
/// The persisted shape of a reconnect session in Redis, stored as a single JSON string value
/// under an unguessable, server-generated token. Possessing the token is sufficient to resume
/// this exact participant identity in this exact room.
/// </summary>
public sealed record RoomSession(string RoomId, string ParticipantId, string DisplayName);