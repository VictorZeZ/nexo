using nexo.Options;
using nexo.Redis;
using nexo.Rooms.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace nexo.Rooms;

/// <summary>
/// Owns server-side room state in Redis: creation, membership, capacity, private-room
/// password verification, and reconnect sessions. Membership is connection-scoped in the sense
/// that ParticipantId has no meaning outside a room, but a participant identity can now persist
/// across a reconnect when a valid session token is presented (see RoomMessageHandler).
/// </summary>
public sealed class RoomManager(IConnectionMultiplexer redis, RoomSettings settings, ILogger<RoomManager> logger)
{
    private const int MaxCreateAttempts = 5;

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

    // Removes a participant and, once the room becomes empty, deletes all of its state (metadata,
    // participants, chat history and message-ID set) rather than leaving encrypted history behind
    // to rely solely on TTL expiry.
    private const string RemoveParticipantScript = """
        local roomKey = KEYS[1]
        local participantsKey = KEYS[2]
        local historyKey = KEYS[3]
        local messageIdsKey = KEYS[4]
        local participantId = ARGV[1]

        redis.call('SREM', participantsKey, participantId)
        local remaining = redis.call('SCARD', participantsKey)
        if remaining == 0 then
            redis.call('DEL', roomKey, participantsKey, historyKey, messageIdsKey)
        end
        return remaining
        """;

    // Atomically counts a failed password attempt and starts the window on the first failure.
    private const string RecordFailedJoinScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], tonumber(ARGV[1]))
        end
        return count
        """;

    /// <summary>
    /// Creates a new room with a freshly generated, unique join code. Does not add the creator
    /// as a participant — call <see cref="JoinRoomAsync"/> afterward for that.
    /// </summary>
    public async Task<RoomCreateResult> CreateRoomAsync(
        string roomName,
        string creatorParticipantId,
        string creatorDisplayName,
        RoomVisibility visibility,
        string? password,
        CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var timeToLive = TimeSpan.FromMinutes(settings.TimeToLiveMinutes);

        var record = new RoomRecord(
            RoomName: roomName,
            Visibility: visibility,
            PasswordHash: visibility == RoomVisibility.Private ? RoomPasswordHasher.Hash(password!) : null,
            CreatorParticipantId: creatorParticipantId,
            CreatorDisplayName: creatorDisplayName,
            MaxParticipants: settings.DefaultMaxParticipants,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        var serializedRecord = JsonSerializer.Serialize(record);

        try
        {
            for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
            {
                var roomId = RoomIdGenerator.Generate();

                var created = await database.StringSetAsync(
                    RedisKeys.Room(roomId),
                    serializedRecord,
                    timeToLive,
                    When.NotExists);

                if (created)
                {
                    return RoomCreateResult.Created(roomId, roomName, visibility);
                }
            }

            logger.LogError("Failed to generate a unique room ID after {Attempts} attempts.", MaxCreateAttempts);
            return RoomCreateResult.Failed(RoomCreateOutcome.TemporarilyUnavailable);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while creating room {RoomName}.", roomName);
            return RoomCreateResult.Failed(RoomCreateOutcome.TemporarilyUnavailable);
        }
    }

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
            var existing = await database.StringGetAsync(roomKey);
            if (existing.IsNullOrEmpty)
            {
                return RoomJoinResult.Failed(RoomJoinOutcome.RoomNotFound);
            }

            room = JsonSerializer.Deserialize<RoomRecord>((string)existing!)
                ?? throw new InvalidOperationException("Stored room record deserialized to null.");

            var isAlreadyMember = await database.SetContainsAsync(participantsKey, participantId);

            // An already-verified member (e.g. resuming a session) never needs to re-supply a
            // private room's password; the session token itself is the credential at that point.
            if (!isAlreadyMember && room.Visibility == RoomVisibility.Private)
            {
                var passwordFailure = await CheckPrivateRoomPasswordAsync(database, roomId, room, password);
                if (passwordFailure is { } failureOutcome)
                {
                    return RoomJoinResult.Failed(failureOutcome);
                }
            }
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while reading room {RoomId}.", roomId);
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

        return RoomJoinResult.Joined(room.Visibility, room.RoomName);
    }

    /// <summary>
    /// Verifies a private room's password while enforcing the failed-attempt cap. Returns null when
    /// the password is correct, otherwise the outcome to report. The cap is read before hashing and
    /// incremented only on a wrong password, so attempts already in flight when it is reached can
    /// overshoot it slightly; every later attempt in the window is rejected without being hashed.
    /// </summary>
    private async Task<RoomJoinOutcome?> CheckPrivateRoomPasswordAsync(IDatabase database, string roomId, RoomRecord room, string? password)
    {
        // A missing password is rejected without hashing and without counting as a failed attempt,
        // so it cannot be used to lock legitimate joiners out of the room.
        if (string.IsNullOrEmpty(password) || room.PasswordHash is null)
        {
            return RoomJoinOutcome.InvalidPassword;
        }

        var failedJoinsKey = RedisKeys.RoomFailedJoins(roomId);

        var recordedFailures = await database.StringGetAsync(failedJoinsKey);
        var failedAttempts = recordedFailures.HasValue ? (long)recordedFailures : 0;
        if (failedAttempts >= settings.MaxFailedJoinAttempts)
        {
            logger.LogWarning("Room {RoomId} has reached its failed password attempt limit.", roomId);
            return RoomJoinOutcome.TooManyAttempts;
        }

        if (RoomPasswordHasher.Verify(password, room.PasswordHash))
        {
            return null;
        }

        await database.ScriptEvaluateAsync(RecordFailedJoinScript, [failedJoinsKey], [settings.FailedJoinWindowSeconds]);
        return RoomJoinOutcome.InvalidPassword;
    }

    /// <summary>
    /// Removes a participant from a room (deleting all room state if it becomes empty) and
    /// invalidates their reconnect token, so a participant who has left cannot resume into the
    /// room later.
    /// </summary>
    public async Task<bool> LeaveRoomAsync(string roomId, string participantId, string? reconnectToken, CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();

        try
        {
            await database.ScriptEvaluateAsync(
                RemoveParticipantScript,
                [
                    RedisKeys.Room(roomId),
                    RedisKeys.RoomParticipants(roomId),
                    RedisKeys.RoomHistory(roomId),
                    RedisKeys.RoomMessageIds(roomId)
                ],
                [participantId]);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while removing participant from room {RoomId}.", roomId);
            return false;
        }

        if (reconnectToken is not null)
        {
            await DeleteSessionAsync(reconnectToken, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Invalidates a reconnect token. A failure is non-fatal: the token still expires on its own
    /// when its TTL elapses.
    /// </summary>
    public async Task DeleteSessionAsync(string reconnectToken, CancellationToken cancellationToken)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(RedisKeys.Session(reconnectToken));
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while invalidating a reconnect session.");
        }
    }

    /// <summary>
    /// Issues a fresh, unguessable reconnect token for a participant's current room session.
    /// A new token replaces the need for any previous one (rotate-on-issue).
    /// </summary>
    public async Task<string> CreateSessionAsync(string roomId, string participantId, string displayName, CancellationToken cancellationToken)
    {
        var token = SessionTokenGenerator.Generate();

        try
        {
            var database = redis.GetDatabase();
            var session = new RoomSession(roomId, participantId, displayName);
            var timeToLive = TimeSpan.FromMinutes(settings.TimeToLiveMinutes);

            await database.StringSetAsync(RedisKeys.Session(token), JsonSerializer.Serialize(session), timeToLive);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while creating a reconnect session for room {RoomId}.", roomId);
            // Non-fatal: the client just won't be able to resume if Redis is briefly down; they
            // can still rejoin normally with SetDisplayName + JoinRoom.
        }

        return token;
    }

    /// <summary>
    /// Resolves and consumes a reconnect token in one step (a token can only ever be used once;
    /// a fresh one is always issued afterward via <see cref="CreateSessionAsync"/>).
    /// </summary>
    public async Task<RoomSession?> TryResumeSessionAsync(string reconnectToken, CancellationToken cancellationToken)
    {
        try
        {
            var database = redis.GetDatabase();
            var raw = await database.StringGetDeleteAsync(RedisKeys.Session(reconnectToken));

            return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RoomSession>((string)raw!);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis unavailable while resolving a reconnect session.");
            return null;
        }
    }

    /// <summary>
    /// Extends the Redis TTL of every room and reconnect session that still has a live connection,
    /// in a single pipelined round trip. Without this, a room that stays active longer than
    /// <see cref="RoomSettings.TimeToLiveMinutes"/> would silently lose its state underneath its
    /// participants. A failure is non-fatal: the next sweep simply tries again.
    /// </summary>
    public async Task RefreshActiveStateAsync(
        IReadOnlyCollection<string> roomIds,
        IReadOnlyCollection<string> reconnectTokens,
        CancellationToken cancellationToken)
    {
        if (roomIds.Count == 0 && reconnectTokens.Count == 0)
        {
            return;
        }

        var timeToLive = TimeSpan.FromMinutes(settings.TimeToLiveMinutes);
        var batch = redis.GetDatabase().CreateBatch();
        var pending = new List<Task>(roomIds.Count * 2 + reconnectTokens.Count);

        foreach (var roomId in roomIds)
        {
            pending.Add(batch.KeyExpireAsync(RedisKeys.Room(roomId), timeToLive));
            pending.Add(batch.KeyExpireAsync(RedisKeys.RoomParticipants(roomId), timeToLive));
        }

        foreach (var reconnectToken in reconnectTokens)
        {
            pending.Add(batch.KeyExpireAsync(RedisKeys.Session(reconnectToken), timeToLive));
        }

        try
        {
            batch.Execute();
            await Task.WhenAll(pending).WaitAsync(cancellationToken);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis unavailable while refreshing the TTL of active rooms and sessions.");
        }
    }
}