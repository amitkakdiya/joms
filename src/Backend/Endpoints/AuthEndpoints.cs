using Joms.DTOs;
using Joms.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace Joms.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
        {
            var result = await service.LoginAsync(request, ct);
            return result is null
                ? Results.Unauthorized()
                : Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .WithName("Login")
        .WithSummary("Authenticate a user with their database username and password");

        return app;
    }
}
