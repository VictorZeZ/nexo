using System.Security.Cryptography;

namespace nexo.Rooms;

/// <summary>
/// Hashes and verifies private-room passwords using PBKDF2-HMAC-SHA256. Passwords are never
/// stored, compared, or logged in plaintext.
/// </summary>
public static class RoomPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int SubkeySizeBytes = 32;
    private const int Iterations = 210_000; // OWASP-recommended minimum for PBKDF2-HMAC-SHA256.
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Produces a self-describing, base64-encoded hash safe to store in Redis.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, SubkeySizeBytes);

        var result = new byte[1 + sizeof(int) + SaltSizeBytes + SubkeySizeBytes];
        result[0] = 0x01; // format version, allows future algorithm changes without breaking old hashes
        BitConverter.GetBytes(Iterations).CopyTo(result, 1);
        salt.CopyTo(result, 1 + sizeof(int));
        subkey.CopyTo(result, 1 + sizeof(int) + SaltSizeBytes);

        return Convert.ToBase64String(result);
    }

    /// <summary>Verifies a plaintext password against a previously produced hash, in fixed time.</summary>
    public static bool Verify(string password, string hash)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length != 1 + sizeof(int) + SaltSizeBytes + SubkeySizeBytes || decoded[0] != 0x01)
        {
            return false;
        }

        var iterations = BitConverter.ToInt32(decoded, 1);
        var salt = decoded[(1 + sizeof(int))..(1 + sizeof(int) + SaltSizeBytes)];
        var expectedSubkey = decoded[(1 + sizeof(int) + SaltSizeBytes)..];

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, SubkeySizeBytes);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}