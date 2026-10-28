using System.Collections.Concurrent;

namespace nexo.WebSockets.Connection;

/// <summary>
/// Tracks every live WebSocket connection from the moment it is accepted until it closes,
/// whether or not it has joined a room, so connection health monitoring also covers connections
/// that never join one. Process-local, like RoomConnectionRegistry.
/// </summary>
public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, WebSocketConnection> _connections = new();

    public void Add(WebSocketConnection connection) => _connections[connection.ConnectionId] = connection;

    public void Remove(WebSocketConnection connection) => _connections.TryRemove(connection.ConnectionId, out _);

    /// <summary>A point-in-time snapshot, safe to enumerate while connections come and go.</summary>
    public IReadOnlyCollection<WebSocketConnection> GetAll() => _connections.Values.ToArray();
}