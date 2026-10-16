using System.Data;
using Dapper;
using Joms.DTOs;
using Joms.Infrastructure;

namespace Joms.Repositories;

public sealed class NotificationOutboxRepository(ISqlConnectionFactory factory)
    : INotificationOutboxRepository
{
    public async Task<IReadOnlyList<NotificationOutboxDto>> ClaimAsync(CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        var messages = await db.QueryAsync<NotificationOutboxDto>(new CommandDefinition(
            "dbo.SP_NotificationOutbox_Claim",
            new { BatchSize = 25, LeaseSeconds = 60 },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
        return messages.AsList();
    }

    public async Task MarkProcessedAsync(long outboxId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        await db.ExecuteAsync(new CommandDefinition(
            "dbo.SP_NotificationOutbox_MarkProcessed",
            new { OutboxId = outboxId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
    }

    public async Task ScheduleRetryAsync(long outboxId, string error, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        await db.ExecuteAsync(new CommandDefinition(
            "dbo.SP_NotificationOutbox_ScheduleRetry",
            new { OutboxId = outboxId, Error = error.Length > 1000 ? error[..1000] : error },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
    }
}
