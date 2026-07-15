using Application.DTOs;
using Application.DTOs.Notifications;
using Domain.Enums;

namespace Application.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetAllAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all notifications older than the given number of days (system-wide).
    /// Returns the number of notifications removed.
    /// </summary>
    Task<int> DeleteOlderThanAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates notifications for the given recipients and pushes them via SignalR.
    /// </summary>
    Task NotifyAsync(
        NotificationType type,
        Guid actorId,
        string actorName,
        Guid? taskId,
        string? taskTitle,
        string message,
        IEnumerable<Guid> recipientUserIds,
        CancellationToken cancellationToken = default);
}
