using nexo.Options;
using nexo.Redis;
using nexo.Rooms.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace nexo.Rooms;

/// <summary>
/// Owns server-side room state in Redis: creation, membership, capacity, and private-room
/// password verification. Membership here is connection-scoped — no permanent user identity
/// system exists yet, so a participant ID is only as durable as the connection that owns it.
/// </summary>
public sealed class RoomManager(IConnectionMultiplexer redis, RoomSettings settings, ILogger<RoomManager> logger)
{
    // Atomically enforces room capacity before adding a participant. Rejoining (same participant
    // already a member) is idempotent and never counted twice.
    private const string AddParticipantScript = """
        local participantsKey = KEYS[1]
        local participantId = ARGV[1]
        local maxParticipants = tonumber(ARGV[2])

        if redis.call('SISMEMBER', participantsKey, participantId) == 1 then
            return 1
        end

        if redis.call('SCARD', participantsKey) >= maxParticipants then
            return 0
        end

        redis.call('SADD', participantsKey, participantId)
        return 1
        """;

    // Removes a participant and deletes the room entirely once it becomes empty, rather than
    // leaving empty room state to rely solely on TTL expiry.
    private const string RemoveParticipantScript = """
        local roomKey = KEYS[1]
        local participantsKey = KEYS[2]
        local participantId = ARGV[1]

        redis.call('SREM', participantsKey, participantId)
        local remaining = redis.call('SCARD', participantsKey)
        if remaining == 0 then
            redis.call('DEL', roomKey)
            redis.call('DEL', participantsKey)
        end
        return remaining
        """;

    public async Task<RoomJoinResult> JoinRoomAsync(
        string roomId,
        string participantId,
        string? password,
        CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var roomKey = RedisKeys.Room(roomId);
        var participantsKey = RedisKeys.RoomParticipants(roomId);
        var timeToLive = TimeSpan.FromMinutes(settings.TimeToLiveMinutes);

        RoomRecord room;

        try
        {
            var candidateRoom = new RoomRecord(
                Visibility: string.IsNullOrEmpty(password) ? RoomVisibility.Public : RoomVisibility.Private,
                PasswordHash: string.IsNullOrEmpty(password) ? null : RoomPasswordHasher.Hash(password),
                MaxParticipants: settings.DefaultMaxParticipants,
                CreatedAtUtc: DateTimeOffset.UtcNow);

            var createdNow = await database.StringSetAsync(
                roomKey,
                JsonSerializer.Serialize(candidateRoom),
                timeToLive,
                When.NotExists);

            if (createdNow)
            {
                room = candidateRoom;
            }
            else
            {
                var existing = await database.StringGetAsync(roomKey);
                if (existing.IsNullOrEmpty)
                {
                    // Extremely rare race: the room expired between our create attempt and this read.
                    logger.LogWarning("Room {RoomId} disappeared between creation check and read.", roomId);
                    return RoomJoinResult.Failed(RoomJoinOutcome.TemporarilyUnavailable);
                }

                room = JsonSerializer.Deserialize<RoomRecord>((string)existing!)
                    ?? throw new InvalidOperationException("Stored room record deserialized to null.");

                if (room.Visibility == RoomVisibility.Private &&
                    (string.IsNullOrEmpty(password) || room.PasswordHash is null || !RoomPasswordHasher.Verify(password, room.PasswordHash)))
                {
                    return RoomJoinResult.Failed(RoomJoinOutcome.InvalidPassword);
                }
            }
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while ensuring room {RoomId} exists.", roomId);
            return RoomJoinResult.Failed(RoomJoinOutcome.TemporarilyUnavailable);
        }

        try
        {
            var addResult = await database.ScriptEvaluateAsync(
                AddParticipantScript,
                [participantsKey],
                [participantId, room.MaxParticipants]);

            if ((int)addResult != 1)
            {
                return RoomJoinResult.Failed(RoomJoinOutcome.RoomFull);
            }

            await database.KeyExpireAsync(participantsKey, timeToLive);
            await database.KeyExpireAsync(roomKey, timeToLive);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while adding participant to room {RoomId}.", roomId);
            return RoomJoinResult.Failed(RoomJoinOutcome.TemporarilyUnavailable);
        }

        return RoomJoinResult.Joined(room.Visibility);
    }

    public async Task<bool> LeaveRoomAsync(string roomId, string participantId, CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var roomKey = RedisKeys.Room(roomId);
        var participantsKey = RedisKeys.RoomParticipants(roomId);

        try
        {
            await database.ScriptEvaluateAsync(RemoveParticipantScript, [roomKey, participantsKey], [participantId]);
            return true;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while removing participant from room {RoomId}.", roomId);
            return false;
        }
    }
}