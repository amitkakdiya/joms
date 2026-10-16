namespace Joms.DTOs;

public sealed record NotificationDto(
    long NotificationId,
    long UserId,
    long OrderId,
    string EventType,
    string Title,
    string Message,
    string? ActionUrl,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    long TotalCount,
    int Page,
    int PageSize);

public sealed record UnreadCountDto(long Count);
public sealed record MarkReadResultDto(bool Updated);
/// <summary>Customer-provided details for an order submission.</summary>
public sealed record CreateOrderRequest(string? Description);
/// <summary>CAD user selected by an administrator for an order.</summary>
public sealed record AssignCadRequest(long CadUserId);
public sealed record DesignDecisionRequest(string? Comment);
/// <summary>Active production or billing user selected for an order assignment.</summary>
public sealed record AssignWorkflowUserRequest(long UserId);
public sealed record WorkflowResultDto(long OrderId, string Status);
/// <summary>Order details visible to the current customer or an administrator.</summary>
public sealed record OrderListItemDto(
    long OrderId,
    long CustomerUserId,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long? AssignedCadUserId,
    string? AssignedCadDisplayName,
    long? AssignedUserId,
    string? AssignedDisplayName,
    string? AssignedRole);
/// <summary>Active CAD account available for order assignment.</summary>
public sealed record CadUserDto(long UserId, string DisplayName);
/// <summary>Active workflow account available for order assignment.</summary>
public sealed record WorkflowUserDto(long UserId, string DisplayName);

public sealed record NotificationOutboxDto(
    long OutboxId,
    long NotificationId,
    long UserId,
    long OrderId,
    string EventType,
    string Title,
    string Message,
    string? ActionUrl,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    int Attempts);
