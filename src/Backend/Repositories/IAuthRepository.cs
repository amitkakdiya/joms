using Joms.DTOs;

namespace Joms.Repositories;

public interface IAuthRepository
{
    Task<UserCredentialDto?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<long?> CreateUserAsync(
        string username,
        string passwordHash,
        string passwordSalt,
        string displayName,
        string role,
        CancellationToken ct);
    Task UpgradeLegacyPasswordAsync(
        long userId,
        string passwordHash,
        string passwordSalt,
        CancellationToken ct);
}
