using Application.DTOs.Notifications;

namespace Application.Interfaces;

/// <summary>
/// Abstraction for pushing real-time notifications to connected clients.
/// </summary>
public interface INotificationPusher
{
    Task PushToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
