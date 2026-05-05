using Domain.Enums;

namespace Domain.Entities;

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? SourceTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User CreatedBy { get; set; } = null!;
    public Template? SourceTemplate { get; set; }
    public ICollection<User> Assignees { get; set; } = new List<User>();
    public ICollection<TaskStep> Steps { get; set; } = new List<TaskStep>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
}
