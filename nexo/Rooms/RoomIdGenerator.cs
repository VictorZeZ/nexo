using System.Security.Cryptography;

namespace nexo.Rooms;

/// <summary>
/// Generates short, random join codes for newly created rooms. Collisions are vanishingly rare
/// given the code space, but RoomManager still verifies uniqueness in Redis and retries on the
/// rare chance of one.
/// </summary>
public static class RoomIdGenerator
{
    // Excludes visually ambiguous characters (0/O, 1/I/L) to keep codes easy to read and share aloud.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public static string Generate()
    {
        Span<char> code = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}