namespace Application.DTOs.Companies;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
}
