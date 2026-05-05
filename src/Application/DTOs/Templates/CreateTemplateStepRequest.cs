namespace Application.DTOs.Templates;

public class CreateTemplateStepRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
}
