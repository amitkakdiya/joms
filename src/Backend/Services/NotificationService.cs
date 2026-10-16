using Joms.DTOs;
using Joms.Repositories;

namespace Joms.Services;

public sealed class NotificationService(INotificationRepository repository) : INotificationService
{
    public Task<NotificationPageDto> GetAsync(long userId, int page, int pageSize, CancellationToken ct) =>
        repository.GetAsync(userId, page, pageSize, ct);

    public Task<long> GetUnreadCountAsync(long userId, CancellationToken ct) =>
        repository.GetUnreadCountAsync(userId, ct);

    public Task<bool> MarkReadAsync(long userId, long notificationId, CancellationToken ct) =>
        repository.MarkReadAsync(userId, notificationId, ct);

    public Task<int> MarkAllReadAsync(long userId, CancellationToken ct) =>
        repository.MarkAllReadAsync(userId, ct);
}
