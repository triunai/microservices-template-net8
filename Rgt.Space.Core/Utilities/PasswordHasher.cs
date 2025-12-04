using System.Security.Cryptography;

namespace Rgt.Space.Core.Utilities;

/// <summary>
/// Password hashing utility using HMAC-SHA512.
/// Simple, secure, no external dependencies.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 32; // 256 bits
    private const int HashSize = 64; // 512 bits (SHA-512)
    
    /// <summary>
    /// Hashes a password using HMAC-SHA512 with a random salt.
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <returns>Tuple of (hash, salt)</returns>
    public static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        // Generate a random salt
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        
        // Compute hash
        using var hmac = new HMACSHA512(salt);
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        
        return (hash, salt);
    }
    
    /// <summary>
    /// Verifies a password against a stored hash and salt.
    /// </summary>
    /// <param name="password">Plain text password to verify</param>
    /// <param name="storedHash">Previously stored hash</param>
    /// <param name="storedSalt">Previously stored salt</param>
    /// <returns>True if password matches</returns>
    public static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        if (storedHash.Length != HashSize)
            return false;
        
        using var hmac = new HMACSHA512(storedSalt);
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        
        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
}
