using System.Data;
using Dapper;
using Joms.DTOs;
using Joms.Infrastructure;

namespace Joms.Repositories;

public sealed class AuthRepository(ISqlConnectionFactory factory) : IAuthRepository
{
    public async Task<UserCredentialDto?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<UserCredentialDto>(new CommandDefinition(
            "dbo.SP_User_GetForAuthentication",
            new { Username = username },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
    }

    public async Task<long?> CreateUserAsync(
        string username,
        string passwordHash,
        string passwordSalt,
        string displayName,
        string role,
        CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<long?>(new CommandDefinition(
            "dbo.SP_User_CreateWithCredentials",
            new
            {
                Username = username,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                DisplayName = displayName,
                RoleName = role
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
    }

    public async Task UpgradeLegacyPasswordAsync(
        long userId,
        string passwordHash,
        string passwordSalt,
        CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        await db.ExecuteAsync(new CommandDefinition(
            "dbo.SP_User_UpdateCredentials",
            new { UserId = userId, PasswordHash = passwordHash, PasswordSalt = passwordSalt },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
    }
}
