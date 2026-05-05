namespace Application.DTOs.Templates;

public class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CreateTemplateStepRequest> Steps { get; set; } = [];
}
