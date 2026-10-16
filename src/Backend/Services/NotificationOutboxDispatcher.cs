using Joms.DTOs;
using Joms.Hubs;
using Joms.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace Joms.Services;

public sealed class NotificationOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    ILogger<NotificationOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification outbox polling failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<INotificationOutboxRepository>();
        var messages = await outbox.ClaimAsync(ct);

        foreach (var message in messages)
            await DispatchAsync(outbox, message, ct);
    }

    private async Task DispatchAsync(
        INotificationOutboxRepository outbox,
        NotificationOutboxDto message,
        CancellationToken ct)
    {
        try
        {
            await hub.Clients.User(message.UserId.ToString())
                .SendAsync("ReceiveNotification", ToNotification(message), ct);
            await outbox.MarkProcessedAsync(message.OutboxId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Failed to deliver notification outbox item {OutboxId} to user {UserId}; retrying.",
                message.OutboxId, message.UserId);

            try
            {
                await outbox.ScheduleRetryAsync(message.OutboxId, exception.ToString(), ct);
            }
            catch (Exception retryException)
            {
                logger.LogError(retryException,
                    "Could not schedule retry for notification outbox item {OutboxId}.",
                    message.OutboxId);
            }
        }
    }

    private static NotificationDto ToNotification(NotificationOutboxDto message) =>
        new(message.NotificationId, message.UserId, message.OrderId, message.EventType,
            message.Title, message.Message, message.ActionUrl, message.IsRead,
            message.CreatedAt, message.ReadAt);
}
