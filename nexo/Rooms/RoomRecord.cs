using nexo.Rooms.Models;

namespace nexo.Rooms;

/// <summary>
/// The persisted shape of a room's metadata in Redis, stored as a single JSON string value.
/// Internal to <see cref="RoomManager"/> — callers only ever see <see cref="RoomJoinResult"/>.
/// </summary>
internal sealed record RoomRecord(RoomVisibility Visibility, string? PasswordHash, int MaxParticipants, DateTimeOffset CreatedAtUtc);