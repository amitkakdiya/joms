namespace Joms.Services;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash, string passwordSalt);
}
