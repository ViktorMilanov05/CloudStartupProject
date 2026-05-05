namespace Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
