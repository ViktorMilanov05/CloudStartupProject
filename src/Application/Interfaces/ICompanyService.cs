using Application.DTOs.Companies;

namespace Application.Interfaces;

public interface ICompanyService
{
    Task<List<CompanyDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);
}
