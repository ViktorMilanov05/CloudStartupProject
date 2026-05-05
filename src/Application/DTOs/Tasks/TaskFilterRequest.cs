namespace Application.DTOs.Tasks;

public class TaskFilterRequest
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
