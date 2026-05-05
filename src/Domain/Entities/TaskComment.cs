namespace Domain.Entities;

public class TaskComment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public User Author { get; set; } = null!;
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
}
