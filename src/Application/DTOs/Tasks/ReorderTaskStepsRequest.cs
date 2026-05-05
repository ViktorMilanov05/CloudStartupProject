namespace Application.DTOs.Tasks;

public class ReorderTaskStepsRequest
{
    public List<Guid> StepIds { get; set; } = [];
}
