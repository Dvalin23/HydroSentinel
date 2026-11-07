using System.Security.Cryptography;
using System.Text;

namespace HydroSentinel.Backend;

public static class DeviceKeys
{
    /// <summary>Random 128-bit key as 32 hex chars. Shown once, then only its hash is stored.</summary>
    public static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string Hash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public static bool Matches(string suppliedKey, string storedHash)
    {
        var a = Encoding.UTF8.GetBytes(Hash(suppliedKey));
        var b = Encoding.UTF8.GetBytes(storedHash ?? string.Empty);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static bool SecretMatches(string? supplied, string? expected)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(expected)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
