using System.Data;
using Dapper;
using Joms.DTOs;
using Joms.Infrastructure;

namespace Joms.Repositories;

public sealed class OrderWorkflowRepository(ISqlConnectionFactory factory) : IOrderWorkflowRepository
{
    public async Task<IReadOnlyList<OrderListItemDto>> GetOrdersAsync(
        long userId,
        string role,
        CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        var orders = await db.QueryAsync<OrderListItemDto>(new CommandDefinition(
            "dbo.SP_Order_GetForUser",
            new { UserId = userId, RoleName = role },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
        return orders.AsList();
    }

    public async Task<IReadOnlyList<CadUserDto>> GetCadUsersAsync(CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        var users = await db.QueryAsync<CadUserDto>(new CommandDefinition(
            "dbo.SP_User_GetCadUsers",
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
        return users.AsList();
    }

    public async Task<IReadOnlyList<WorkflowUserDto>> GetProductionUsersAsync(CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        var users = await db.QueryAsync<WorkflowUserDto>(new CommandDefinition(
            "dbo.SP_User_GetProductionUsers",
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
        return users.AsList();
    }

    public async Task<IReadOnlyList<WorkflowUserDto>> GetBillingUsersAsync(CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        var users = await db.QueryAsync<WorkflowUserDto>(new CommandDefinition(
            "dbo.SP_User_GetBillingUsers",
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
        return users.AsList();
    }

    public async Task<WorkflowResultDto> SubmitOrderAsync(long customerId, string? description, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_Submit", new { CustomerId = customerId, Description = description },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> AssignCadAsync(long orderId, long adminId, long cadUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_AssignCad", new { OrderId = orderId, AdminId = adminId, CadUserId = cadUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> SubmitDesignAsync(long orderId, long cadUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_SubmitDesign", new { OrderId = orderId, CadUserId = cadUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> DecideDesignAsync(long orderId, long customerId, bool approved, string? comment, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_DesignDecision", new { OrderId = orderId, CustomerId = customerId, Approved = approved, Comment = comment },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> AssignProductionAsync(long orderId, long adminId, long productionUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_AssignProduction",
            new { OrderId = orderId, AdminId = adminId, ProductionUserId = productionUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> AdvanceProductionAsync(long orderId, long productionUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_AdvanceProduction",
            new { OrderId = orderId, ProductionUserId = productionUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> AssignBillingAsync(long orderId, long productionUserId, long billingUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_AssignBilling",
            new { OrderId = orderId, ProductionUserId = productionUserId, BillingUserId = billingUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> CompleteBillingAsync(long orderId, long billingUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_CompleteBilling",
            new { OrderId = orderId, BillingUserId = billingUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<WorkflowResultDto> MarkDispatchedAsync(long orderId, long billingUserId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<WorkflowResultDto>(new CommandDefinition(
            "dbo.SP_Order_MarkDispatched",
            new { OrderId = orderId, BillingUserId = billingUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }
}
