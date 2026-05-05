using Domain.Enums;

namespace Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
