namespace nexo.Redis;

/// <summary>
/// Centralizes Redis key naming so key formats are defined in exactly one place.
/// Room-specific keys are added here once room state is implemented (Step 5).
/// </summary>
public static class RedisKeys
{
    // Intentionally empty for now — populated once RoomManager needs actual keys.
    // No key formats should be hardcoded elsewhere in the codebase; they belong here.
}