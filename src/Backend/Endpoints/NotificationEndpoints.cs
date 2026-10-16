using System.Security.Claims;
using Joms.DTOs;
using Joms.Services;

namespace Joms.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, INotificationService service,
            int? page, int? pageSize, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var userId)) return Results.Unauthorized();
            return Results.Ok(await service.GetAsync(userId, page ?? 1, pageSize ?? 20, ct));
        }).WithName("GetNotifications").WithSummary("Get the current user's notifications");

        group.MapGet("/unread-count", async (ClaimsPrincipal user, INotificationService service, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var userId)) return Results.Unauthorized();
            return Results.Ok(new UnreadCountDto(await service.GetUnreadCountAsync(userId, ct)));
        }).WithName("GetUnreadNotificationCount").WithSummary("Get the current user's unread notification count");

        group.MapPatch("/{id:long}/read", async (long id, ClaimsPrincipal user,
            INotificationService service, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var userId)) return Results.Unauthorized();
            var updated = await service.MarkReadAsync(userId, id, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).WithName("MarkNotificationRead").WithSummary("Mark one of the current user's notifications as read");

        group.MapPatch("/read-all", async (ClaimsPrincipal user, INotificationService service, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var userId)) return Results.Unauthorized();
            return Results.Ok(new { updatedCount = await service.MarkAllReadAsync(userId, ct) });
        }).WithName("MarkAllNotificationsRead").WithSummary("Mark all of the current user's notifications as read");

        return app;
    }

    internal static bool TryUserId(ClaimsPrincipal user, out long userId) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
}
