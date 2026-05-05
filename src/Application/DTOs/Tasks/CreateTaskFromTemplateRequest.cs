namespace Application.DTOs.Tasks;

public class CreateTaskFromTemplateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<Guid> AssigneeIds { get; set; } = [];
    public string Priority { get; set; } = "Medium";
    public DateTime? DueDate { get; set; }
}
