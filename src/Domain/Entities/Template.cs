namespace Domain.Entities;

public class Template
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CreatedById { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<TemplateStep> Steps { get; set; } = new List<TemplateStep>();
}
