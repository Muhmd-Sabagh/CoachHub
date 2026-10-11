using System.Security.Cryptography;
using System.Text;

namespace CoachHub.Application.Auth.Sessions;

/// <summary>Opaque refresh tokens: 256 random bits, stored only as a SHA-256 hash.</summary>
public static class RefreshTokens
{
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// A plain hash is enough: the token is random and high-entropy, so it cannot be guessed from
    /// the hash, and lookups need a deterministic value.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
