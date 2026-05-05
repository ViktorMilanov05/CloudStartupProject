namespace Domain.Entities;

public class TaskStep
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }

    public TaskItem Task { get; set; } = null!;
    public User? CompletedBy { get; set; }
}
