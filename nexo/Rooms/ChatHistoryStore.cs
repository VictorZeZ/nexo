using nexo.Options;
using nexo.Redis;
using nexo.WebSockets.Protocol.Messages;
using StackExchange.Redis;
using System.Text.Json;

namespace nexo.Rooms;

/// <summary>
/// Stores and retrieves capped, TTL-bound chat history per room in Redis. Entries are opaque
/// ciphertext to this class — it has no knowledge of encryption and never sees plaintext.
/// </summary>
public sealed class ChatHistoryStore(IConnectionMultiplexer redis, RoomSettings settings, ILogger<ChatHistoryStore> logger)
{
    // Atomically de-duplicates by message ID and appends to the capped history list in one step,
    // so a resend after a dropped acknowledgement is never stored or relayed twice.
    private const string StoreScript = """
        local historyKey = KEYS[1]
        local seenIdsKey = KEYS[2]
        local messageId = ARGV[1]
        local entryJson = ARGV[2]
        local maxEntries = tonumber(ARGV[3])
        local ttlSeconds = tonumber(ARGV[4])

        if redis.call('SISMEMBER', seenIdsKey, messageId) == 1 then
            return 0
        end

        redis.call('SADD', seenIdsKey, messageId)
        redis.call('EXPIRE', seenIdsKey, ttlSeconds)

        redis.call('RPUSH', historyKey, entryJson)
        redis.call('LTRIM', historyKey, -maxEntries, -1)
        redis.call('EXPIRE', historyKey, ttlSeconds)

        return 1
        """;

    /// <summary>Stores a message if it hasn't been seen before. Returns false for a duplicate resend.</summary>
    public async Task<bool> TryStoreAsync(string roomId, ChatHistoryEntry entry, CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();

        try
        {
            var result = await database.ScriptEvaluateAsync(
                StoreScript,
                [RedisKeys.RoomHistory(roomId), RedisKeys.RoomMessageIds(roomId)],
                [entry.MessageId.ToString(), JsonSerializer.Serialize(entry), settings.MaxHistoryMessages, settings.TimeToLiveMinutes * 60]);

            return (int)result == 1;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while storing a chat message for room {RoomId}.", roomId);

            // Treat as "new" so the message is still relayed live even if history storage failed;
            // losing history during a brief outage is preferable to silently dropping the message.
            return true;
        }
    }

    public async Task<IReadOnlyList<ChatHistoryEntry>> GetHistoryAsync(string roomId, CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();

        try
        {
            var rawEntries = await database.ListRangeAsync(RedisKeys.RoomHistory(roomId));

            return rawEntries
                .Select(value => JsonSerializer.Deserialize<ChatHistoryEntry>((string)value!))
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .ToList();
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while loading chat history for room {RoomId}.", roomId);
            return [];
        }
    }
}