using System.Security.Cryptography;
using System.Text;

namespace SobeSobe.Api.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;

    public static string HashPassword(string password)
    {
        // Generate salt
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Generate hash
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        // Combine salt and hash
        var hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        // Convert to base64
        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string passwordHash)
    {
        // Convert from base64
        byte[] hashBytes;

        try
        {
            hashBytes = Convert.FromBase64String(passwordHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (hashBytes.Length != SaltSize + HashSize)
        {
            return false;
        }

        // Extract salt
        var salt = hashBytes.AsSpan(0, SaltSize);

        // Compute hash of provided password
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        // Compare hashes in constant time
        var expectedHash = hashBytes.AsSpan(SaltSize, HashSize);
        return CryptographicOperations.FixedTimeEquals(expectedHash, hash);
    }
}
