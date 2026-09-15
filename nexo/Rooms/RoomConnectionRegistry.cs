using nexo.WebSockets.Connection;
using System.Collections.Concurrent;

namespace nexo.Rooms;

/// <summary>
/// Tracks which live WebSocket connections belong to which room, purely in-memory, so incoming
/// room messages can be broadcast to the right connections on this server instance.
/// This state is not shared across server instances. Horizontal scaling would need a cross-instance
/// fan-out mechanism (e.g. Redis Pub/Sub) layered on top of this — out of scope until required.
/// </summary>
public sealed class RoomConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, WebSocketConnection>> _roomConnections = new();

    public void Register(string roomId, WebSocketConnection connection)
    {
        var connections = _roomConnections.GetOrAdd(roomId, _ => new ConcurrentDictionary<Guid, WebSocketConnection>());
        connections[connection.ConnectionId] = connection;
    }

    public void Unregister(string roomId, WebSocketConnection connection)
    {
        if (!_roomConnections.TryGetValue(roomId, out var connections))
        {
            return;
        }

        connections.TryRemove(connection.ConnectionId, out _);

        if (connections.IsEmpty)
        {
            _roomConnections.TryRemove(roomId, out _);
        }
    }

    public IReadOnlyCollection<WebSocketConnection> GetConnections(string roomId) =>
        _roomConnections.TryGetValue(roomId, out var connections)
            ? connections.Values.ToArray()
            : [];
}