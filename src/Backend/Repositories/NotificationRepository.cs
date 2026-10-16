using System.Data;
using Dapper;
using Joms.DTOs;
using Joms.Infrastructure;

namespace Joms.Repositories;

public sealed class NotificationRepository(ISqlConnectionFactory factory) : INotificationRepository
{
    public async Task<NotificationPageDto> GetAsync(long userId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var db = factory.CreateConnection();
        using var multi = await db.QueryMultipleAsync(new CommandDefinition(
            "dbo.SP_Notification_Get", new { UserId = userId, Page = page, PageSize = pageSize },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
        var items = (await multi.ReadAsync<NotificationDto>()).AsList();
        var total = await multi.ReadSingleAsync<long>();
        return new NotificationPageDto(items, total, page, pageSize);
    }

    public async Task<long> GetUnreadCountAsync(long userId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<long>(new CommandDefinition(
            "dbo.SP_Notification_UnreadCount", new { UserId = userId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<bool> MarkReadAsync(long userId, long notificationId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        var rows = await db.QuerySingleAsync<int>(new CommandDefinition(
            "dbo.SP_Notification_MarkRead", new { UserId = userId, NotificationId = notificationId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<int> MarkAllReadAsync(long userId, CancellationToken ct)
    {
        await using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<int>(new CommandDefinition(
            "dbo.SP_Notification_MarkAllRead", new { UserId = userId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }
}
