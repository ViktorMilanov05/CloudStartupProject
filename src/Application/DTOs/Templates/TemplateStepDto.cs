namespace Application.DTOs.Templates;

public class TemplateStepDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }
}
