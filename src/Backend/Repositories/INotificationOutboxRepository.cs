using Joms.DTOs;

namespace Joms.Repositories;

public interface INotificationOutboxRepository
{
    Task<IReadOnlyList<NotificationOutboxDto>> ClaimAsync(CancellationToken ct);
    Task MarkProcessedAsync(long outboxId, CancellationToken ct);
    Task ScheduleRetryAsync(long outboxId, string error, CancellationToken ct);
}
