namespace Application.DTOs.Tasks;

public class CreateTaskStepRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
}
