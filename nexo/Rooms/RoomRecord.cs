using nexo.Rooms.Models;

namespace nexo.Rooms;

/// <summary>
/// The persisted shape of a room's metadata in Redis, stored as a single JSON string value.
/// Internal to <see cref="RoomManager"/> — callers only ever see <see cref="RoomJoinResult"/>
/// and <see cref="RoomCreateResult"/>.
/// </summary>
internal sealed record RoomRecord(string RoomName, RoomVisibility Visibility, string? PasswordHash, string CreatorParticipantId, string CreatorDisplayName, int MaxParticipants, DateTimeOffset CreatedAtUtc);