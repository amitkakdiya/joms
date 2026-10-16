using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Joms.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    // No client-controlled user/group joining. User targeting comes from JWT NameIdentifier.
}

public sealed class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
