namespace Application.DTOs.Companies;

public class CreateCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;
    public string ManagerPassword { get; set; } = string.Empty;
    public string ManagerFirstName { get; set; } = string.Empty;
    public string ManagerLastName { get; set; } = string.Empty;
}
