using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Joms.DTOs;
using Joms.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace Joms.Services;

public sealed class AuthService(
    IAuthRepository repository,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : IAuthService
{
    private static readonly HashSet<string> AllowedRoles =
        new(["Admin", "Customer", "CAD", "Production", "Billing"], StringComparer.OrdinalIgnoreCase);

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrEmpty(request.Password) ||
            request.Password.Length > 256)
            return null;

        var user = await repository.GetByUsernameAsync(request.Username.Trim(), ct);
        if (user is null)
        {
            VerifyUnknownUserPassword(request.Password);
            return null;
        }

        var passwordVerified = user.PasswordHash is not null && user.PasswordSalt is not null
            ? passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt)
            : user.LegacyPassword is not null && VerifyLegacyPassword(request.Password, user.LegacyPassword);
        if (!passwordVerified)
            return null;

        if (user.PasswordHash is null || user.PasswordSalt is null || user.LegacyPassword is not null)
        {
            var (hash, salt) = passwordHasher.HashPassword(request.Password);
            await repository.UpgradeLegacyPasswordAsync(user.UserId, hash, salt, ct);
        }

        return CreateLoginResponse(user);
    }

    public async Task<DevelopmentUserCreatedResponse?> CreateDevelopmentUserAsync(
        CreateDevelopmentUserRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Role))
            throw new ArgumentException("All user fields are required.");

        var username = request.Username.Trim();
        var displayName = request.DisplayName.Trim();
        var role = AllowedRoles.FirstOrDefault(
            candidate => candidate.Equals(request.Role.Trim(), StringComparison.OrdinalIgnoreCase));

        if (username.Length is < 3 or > 100 ||
            displayName.Length is < 1 or > 150 ||
            request.Password.Length is < 6 or > 256 ||
            role is null)
            throw new ArgumentException(
                "Username is required and must be 3-100 characters; password is required and must be 6-256 characters; display name is required and must be 1-150 characters; role must be Admin, Customer, CAD, Production, or Billing.");

        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var userId = await repository.CreateUserAsync(username, hash, salt, displayName, role, ct);
        return userId is null ? null : new DevelopmentUserCreatedResponse(userId.Value, username);
    }

    private LoginResponse CreateLoginResponse(UserCredentialDto user)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is missing.");
        var secret = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is missing.");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(2);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.RoleName)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAt,
            user.UserId,
            user.Username ?? string.Empty,
            user.DisplayName,
            user.RoleName);
    }

    private static void VerifyUnknownUserPassword(string password)
    {
        var salt = new byte[32];
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, 210_000, HashAlgorithmName.SHA512, 64);
        CryptographicOperations.ZeroMemory(hash);
    }

    private static bool VerifyLegacyPassword(string password, string legacyPassword)
    {
        var providedBytes = Encoding.UTF8.GetBytes(password);
        var storedBytes = Encoding.UTF8.GetBytes(legacyPassword);
        try
        {
            return providedBytes.Length == storedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(providedBytes, storedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(providedBytes);
            CryptographicOperations.ZeroMemory(storedBytes);
        }
    }
}
