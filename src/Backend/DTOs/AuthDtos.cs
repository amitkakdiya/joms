namespace Joms.DTOs;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    long UserId,
    string Username,
    string DisplayName,
    string Role);

public sealed record CreateDevelopmentUserRequest(
    string Username,
    string Password,
    string DisplayName,
    string Role);

public sealed record DevelopmentUserCreatedResponse(long UserId, string Username);

public sealed record UserCredentialDto(
    long UserId,
    string? Username,
    string? PasswordHash,
    string? PasswordSalt,
    string? LegacyPassword,
    string DisplayName,
    string RoleName);
