using System.Security.Cryptography;

namespace EquipFlow.Infrastructure.Persistence;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        var hashBytes = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, HashSize);
        return Convert.ToBase64String(hashBytes);
    }

    public static bool Verify(string password, string hashedPassword)
    {
        try {
            var hashBytes = Convert.FromBase64String(hashedPassword);
            if (hashBytes.Length != SaltSize + HashSize) return false;
            var salt = new byte[SaltSize];
            Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(hash, hashBytes.AsSpan(SaltSize));
        } catch { return false; }
    }
}
