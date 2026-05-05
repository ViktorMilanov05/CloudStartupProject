namespace Domain.Entities;

public class TemplateStep
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }

    public Template Template { get; set; } = null!;
}
