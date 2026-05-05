using API.Hubs;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace API.Services;

public class SignalRNotificationPusher : INotificationPusher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationPusher(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("ReceiveNotification", notification, cancellationToken);
    }
}
