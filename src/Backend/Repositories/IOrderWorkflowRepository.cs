using Joms.DTOs;

namespace Joms.Repositories;

public interface IOrderWorkflowRepository
{
    Task<IReadOnlyList<OrderListItemDto>> GetOrdersAsync(long userId, string role, CancellationToken ct);
    Task<IReadOnlyList<CadUserDto>> GetCadUsersAsync(CancellationToken ct);
    Task<IReadOnlyList<WorkflowUserDto>> GetProductionUsersAsync(CancellationToken ct);
    Task<IReadOnlyList<WorkflowUserDto>> GetBillingUsersAsync(CancellationToken ct);
    Task<WorkflowResultDto> SubmitOrderAsync(long customerId, string? description, CancellationToken ct);
    Task<WorkflowResultDto> AssignCadAsync(long orderId, long adminId, long cadUserId, CancellationToken ct);
    Task<WorkflowResultDto> SubmitDesignAsync(long orderId, long cadUserId, CancellationToken ct);
    Task<WorkflowResultDto> DecideDesignAsync(long orderId, long customerId, bool approved, string? comment, CancellationToken ct);
    Task<WorkflowResultDto> AssignProductionAsync(long orderId, long adminId, long productionUserId, CancellationToken ct);
    Task<WorkflowResultDto> AdvanceProductionAsync(long orderId, long productionUserId, CancellationToken ct);
    Task<WorkflowResultDto> AssignBillingAsync(long orderId, long productionUserId, long billingUserId, CancellationToken ct);
    Task<WorkflowResultDto> CompleteBillingAsync(long orderId, long billingUserId, CancellationToken ct);
    Task<WorkflowResultDto> MarkDispatchedAsync(long orderId, long billingUserId, CancellationToken ct);
}
