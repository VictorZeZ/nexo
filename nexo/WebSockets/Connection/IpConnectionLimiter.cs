using System.Collections.Concurrent;
using System.Net;

namespace nexo.WebSockets.Connection;

/// <summary>
/// Bounds how many concurrent WebSocket connections a single client IP address may hold open,
/// protecting against one source exhausting server resources by opening unlimited connections.
/// Independent of the HTTP-level rate limiter, which only governs the initial upgrade request,
/// not how many connections stay open afterward.
/// </summary>
public sealed class IpConnectionLimiter
{
    private readonly ConcurrentDictionary<IPAddress, int> _connectionCountsByIp = new();

    public bool TryAcquire(IPAddress? ipAddress, int maxConcurrentConnections)
    {
        if (ipAddress is null)
        {
            // No remote address available (e.g. behind a misconfigured proxy). Allow rather than
            // block legitimate traffic — this is one layer of defense, not the only one.
            return true;
        }

        var updated = _connectionCountsByIp.AddOrUpdate(ipAddress, 1, (_, current) => current + 1);

        if (updated <= maxConcurrentConnections)
        {
            return true;
        }

        _connectionCountsByIp.AddOrUpdate(ipAddress, 0, (_, current) => Math.Max(0, current - 1));
        return false;
    }

    public void Release(IPAddress? ipAddress)
    {
        if (ipAddress is null)
        {
            return;
        }

        _connectionCountsByIp.AddOrUpdate(ipAddress, 0, (_, current) => Math.Max(0, current - 1));
    }
}