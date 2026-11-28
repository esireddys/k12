using System.Security.Cryptography;
using K12.Application.Abstractions;

namespace K12.Infrastructure.Security;

/// <summary>
/// PBKDF2 with SHA-256 and a random salt per PIN, stored as "v1.iterations.salt.hash". A 4-digit PIN has few
/// possibilities, so the domain's lockout after wrong tries is what really protects it; the slow hash keeps a
/// leaked table from giving PINs away instantly.
/// </summary>
public sealed class Pbkdf2PinHasher : IPinHasher
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string pin, string stored)
    {
        var parts = (stored ?? string.Empty).Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations) || iterations < 1) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
