using Application.DTOs;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationPusher _pusher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext dbContext, INotificationPusher pusher, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _pusher = pusher;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationDto>> GetAllAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => MapToDto(n))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }

    public async Task DeleteAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(int days, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _dbContext.Notifications
            .Where(n => n.CreatedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task NotifyAsync(
        NotificationType type,
        Guid actorId,
        string actorName,
        Guid? taskId,
        string? taskTitle,
        string message,
        IEnumerable<Guid> recipientUserIds,
        CancellationToken cancellationToken = default)
    {
        // Filter out the actor (no self-notifications)
        var recipients = recipientUserIds.Where(id => id != actorId).Distinct().ToList();
        if (recipients.Count == 0) return;

        var now = DateTime.UtcNow;
        var notifications = recipients.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Message = message,
            TaskId = taskId,
            TaskTitle = taskTitle,
            ActorId = actorId,
            ActorName = actorName,
            IsRead = false,
            CreatedAt = now,
        }).ToList();

        _dbContext.Notifications.AddRange(notifications);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Push real-time to each connected user
        foreach (var notification in notifications)
        {
            try
            {
                await _pusher.PushToUserAsync(notification.UserId, MapToDto(notification), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push notification to user {UserId}", notification.UserId);
            }
        }
    }

    private static NotificationDto MapToDto(Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type.ToString(),
        Message = n.Message,
        TaskId = n.TaskId,
        TaskTitle = n.TaskTitle,
        ActorId = n.ActorId,
        ActorName = n.ActorName,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt,
    };
}
