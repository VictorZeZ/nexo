using System.Security.Cryptography;

namespace nexo.Rooms;

/// <summary>
/// Generates cryptographically random, unguessable reconnect tokens. A token is a bearer
/// credential: possessing it is sufficient to resume a specific participant's room session.
/// </summary>
public static class SessionTokenGenerator
{
    private const int TokenSizeBytes = 32;

    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenSizeBytes));
}