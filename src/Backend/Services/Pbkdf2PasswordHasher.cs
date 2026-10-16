using System.Security.Cryptography;

namespace Joms.Services;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 64;
    private const int Iterations = 210_000;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string password, string passwordHash, string passwordSalt)
    {
        try
        {
            var expectedHash = Convert.FromBase64String(passwordHash);
            var salt = Convert.FromBase64String(passwordSalt);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
            return expectedHash.Length == actualHash.Length &&
                   CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
