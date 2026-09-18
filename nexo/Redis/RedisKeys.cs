namespace nexo.Redis;

/// <summary>
/// Centralizes Redis key naming so key formats are defined in exactly one place.
/// </summary>
public static class RedisKeys
{
    public static string Room(string roomId) => $"room:{roomId}";

    public static string RoomParticipants(string roomId) => $"room:{roomId}:participants";

    public static string RoomHistory(string roomId) => $"room:{roomId}:history";

    public static string RoomMessageIds(string roomId) => $"room:{roomId}:messageIds";

    public static string Session(string reconnectToken) => $"session:{reconnectToken}";
}