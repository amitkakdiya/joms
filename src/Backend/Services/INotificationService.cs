using Joms.DTOs;

namespace Joms.Services;

public interface INotificationService
{
    Task<NotificationPageDto> GetAsync(long userId, int page, int pageSize, CancellationToken ct);
    Task<long> GetUnreadCountAsync(long userId, CancellationToken ct);
    Task<bool> MarkReadAsync(long userId, long notificationId, CancellationToken ct);
    Task<int> MarkAllReadAsync(long userId, CancellationToken ct);
}
