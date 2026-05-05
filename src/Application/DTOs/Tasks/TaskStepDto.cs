namespace Application.DTOs.Tasks;

public class TaskStepDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
}
