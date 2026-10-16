using Joms.DTOs;
using Joms.Repositories;

namespace Joms.Services;

public sealed class OrderWorkflowService(
    IOrderWorkflowRepository orders) : IOrderWorkflowService
{
    public Task<IReadOnlyList<OrderListItemDto>> GetOrdersAsync(long userId, string role, CancellationToken ct)
        => orders.GetOrdersAsync(userId, role, ct);

    public Task<IReadOnlyList<CadUserDto>> GetCadUsersAsync(CancellationToken ct)
        => orders.GetCadUsersAsync(ct);

    public Task<IReadOnlyList<WorkflowUserDto>> GetProductionUsersAsync(CancellationToken ct)
        => orders.GetProductionUsersAsync(ct);

    public Task<IReadOnlyList<WorkflowUserDto>> GetBillingUsersAsync(CancellationToken ct)
        => orders.GetBillingUsersAsync(ct);

    public async Task<WorkflowResultDto> SubmitOrderAsync(long customerId, string? description, CancellationToken ct)
        => await orders.SubmitOrderAsync(customerId, description, ct);

    public async Task<WorkflowResultDto> AssignCadAsync(long orderId, long adminId, long cadUserId, CancellationToken ct)
        => await orders.AssignCadAsync(orderId, adminId, cadUserId, ct);

    public async Task<WorkflowResultDto> SubmitDesignAsync(long orderId, long cadUserId, CancellationToken ct)
        => await orders.SubmitDesignAsync(orderId, cadUserId, ct);

    public async Task<WorkflowResultDto> DecideDesignAsync(long orderId, long customerId, bool approved, string? comment, CancellationToken ct)
        => await orders.DecideDesignAsync(orderId, customerId, approved, comment, ct);

    public async Task<WorkflowResultDto> AssignProductionAsync(long orderId, long adminId, long productionUserId, CancellationToken ct)
        => await orders.AssignProductionAsync(orderId, adminId, productionUserId, ct);

    public async Task<WorkflowResultDto> AdvanceProductionAsync(long orderId, long productionUserId, CancellationToken ct)
        => await orders.AdvanceProductionAsync(orderId, productionUserId, ct);

    public async Task<WorkflowResultDto> AssignBillingAsync(long orderId, long productionUserId, long billingUserId, CancellationToken ct)
        => await orders.AssignBillingAsync(orderId, productionUserId, billingUserId, ct);

    public async Task<WorkflowResultDto> CompleteBillingAsync(long orderId, long billingUserId, CancellationToken ct)
        => await orders.CompleteBillingAsync(orderId, billingUserId, ct);

    public async Task<WorkflowResultDto> MarkDispatchedAsync(long orderId, long billingUserId, CancellationToken ct)
        => await orders.MarkDispatchedAsync(orderId, billingUserId, ct);
}
