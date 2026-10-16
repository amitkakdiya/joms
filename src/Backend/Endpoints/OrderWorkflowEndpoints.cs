using System.Security.Claims;
using Joms.DTOs;
using Joms.Services;

namespace Joms.Endpoints;

public static class OrderWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapOrderWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            var role = new[] { "Admin", "Customer", "CAD", "Production", "Billing" }
                .FirstOrDefault(user.IsInRole);
            if (role is null) return Results.Forbid();
            return Results.Ok(await service.GetOrdersAsync(userId, role, ct));
        }).WithName("GetOrders").WithSummary("List orders visible to the current workflow role");

        group.MapGet("/cad-users", async (ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out _)) return Results.Unauthorized();
            if (!user.IsInRole("Admin")) return Results.Forbid();
            return Results.Ok(await service.GetCadUsersAsync(ct));
        }).WithName("GetCadUsers").WithSummary("List active CAD users for order assignment");

        group.MapGet("/production-users", async (ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out _)) return Results.Unauthorized();
            if (!user.IsInRole("Admin")) return Results.Forbid();
            return Results.Ok(await service.GetProductionUsersAsync(ct));
        }).WithName("GetProductionUsers").WithSummary("List active production users for order assignment");

        group.MapGet("/billing-users", async (ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out _)) return Results.Unauthorized();
            if (!user.IsInRole("Admin") && !user.IsInRole("Production")) return Results.Forbid();
            return Results.Ok(await service.GetBillingUsersAsync(ct));
        }).WithName("GetBillingUsers").WithSummary("List active billing users for production handoff");

        group.MapPost("/submit", async (CreateOrderRequest request, ClaimsPrincipal user,
            IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Customer")) return Results.Forbid();
            var result = await service.SubmitOrderAsync(userId, request.Description, ct);
            return Results.Created($"/api/orders/{result.OrderId}", result);
        }).WithName("SubmitOrder").WithSummary("Submit an order");

        group.MapPost("/{orderId:long}/assign-cad", async (long orderId, AssignCadRequest request,
            ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Admin")) return Results.Forbid();
            if (request.CadUserId <= 0) return Results.BadRequest(new { error = "CadUserId must be positive." });
            return Results.Ok(await service.AssignCadAsync(orderId, userId, request.CadUserId, ct));
        }).WithName("AssignOrderToCad").WithSummary("Assign a submitted order to a CAD user");

        group.MapPost("/{orderId:long}/design/submit", async (long orderId, ClaimsPrincipal user,
            IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("CAD")) return Results.Forbid();
            return Results.Ok(await service.SubmitDesignAsync(orderId, userId, ct));
        }).WithName("SubmitOrderDesign").WithSummary("Submit an order design for customer approval");

        group.MapPost("/{orderId:long}/design/approve", async (long orderId, DesignDecisionRequest request,
            ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Customer")) return Results.Forbid();
            return Results.Ok(await service.DecideDesignAsync(orderId, userId, true, request.Comment, ct));
        }).WithName("ApproveOrderDesign").WithSummary("Approve an order design");

        group.MapPost("/{orderId:long}/design/reject", async (long orderId, DesignDecisionRequest request,
            ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Customer")) return Results.Forbid();
            return Results.Ok(await service.DecideDesignAsync(orderId, userId, false, request.Comment, ct));
        }).WithName("RejectOrderDesign").WithSummary("Reject an order design and request revision");

        group.MapPost("/{orderId:long}/assign-production", async (long orderId, AssignWorkflowUserRequest request,
            ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Admin")) return Results.Forbid();
            if (request.UserId <= 0) return Results.BadRequest(new { error = "UserId must be positive." });
            return Results.Ok(await service.AssignProductionAsync(orderId, userId, request.UserId, ct));
        }).WithName("AssignOrderToProduction").WithSummary("Assign a customer-approved order to production");

        group.MapPost("/{orderId:long}/production/advance", async (long orderId, ClaimsPrincipal user,
            IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Production")) return Results.Forbid();
            return Results.Ok(await service.AdvanceProductionAsync(orderId, userId, ct));
        }).WithName("AdvanceProductionStage").WithSummary("Advance an assigned order to its next production stage");

        group.MapPost("/{orderId:long}/production/assign-billing", async (long orderId, AssignWorkflowUserRequest request,
            ClaimsPrincipal user, IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Production")) return Results.Forbid();
            if (request.UserId <= 0) return Results.BadRequest(new { error = "UserId must be positive." });
            return Results.Ok(await service.AssignBillingAsync(orderId, userId, request.UserId, ct));
        }).WithName("AssignOrderToBilling").WithSummary("Hand off a finished production order to billing");

        group.MapPost("/{orderId:long}/billing/complete", async (long orderId, ClaimsPrincipal user,
            IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Billing")) return Results.Forbid();
            return Results.Ok(await service.CompleteBillingAsync(orderId, userId, ct));
        }).WithName("CompleteOrderBilling").WithSummary("Complete final billing for an assigned order");

        group.MapPost("/{orderId:long}/dispatch", async (long orderId, ClaimsPrincipal user,
            IOrderWorkflowService service, CancellationToken ct) =>
        {
            if (!NotificationEndpoints.TryUserId(user, out var userId)) return Results.Unauthorized();
            if (!user.IsInRole("Billing")) return Results.Forbid();
            return Results.Ok(await service.MarkDispatchedAsync(orderId, userId, ct));
        }).WithName("MarkOrderDispatched").WithSummary("Mark a billed order as dispatched");

        return app;
    }
}
