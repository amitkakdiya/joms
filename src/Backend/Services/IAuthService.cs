using Joms.DTOs;

namespace Joms.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<DevelopmentUserCreatedResponse?> CreateDevelopmentUserAsync(
        CreateDevelopmentUserRequest request,
        CancellationToken ct);
}
