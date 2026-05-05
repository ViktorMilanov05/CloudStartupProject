namespace Application.DTOs.Tasks;

public class TaskItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public List<TaskAssigneeDto> Assignees { get; set; } = [];
    public Guid? SourceTemplateId { get; set; }
    public string? SourceTemplateName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int StepCount { get; set; }
    public int CompletedStepCount { get; set; }
}
