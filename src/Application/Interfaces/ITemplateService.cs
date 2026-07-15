using Application.DTOs.Templates;

namespace Application.Interfaces;

public interface ITemplateService
{
    Task<List<TemplateDto>> GetAllAsync(Guid companyId, bool isAdmin, bool? isActive, CancellationToken cancellationToken = default);
    Task<TemplateDetailDto?> GetByIdAsync(Guid id, Guid companyId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<TemplateDetailDto> CreateAsync(Guid createdById, Guid companyId, CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TemplateDetailDto> UpdateAsync(Guid id, Guid companyId, bool isAdmin, UpdateTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid companyId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<TemplateStepDto> AddStepAsync(Guid templateId, Guid companyId, bool isAdmin, CreateTemplateStepRequest request, CancellationToken cancellationToken = default);
    Task<TemplateStepDto> UpdateStepAsync(Guid templateId, Guid stepId, Guid companyId, bool isAdmin, UpdateTemplateStepRequest request, CancellationToken cancellationToken = default);
    Task DeleteStepAsync(Guid templateId, Guid stepId, Guid companyId, bool isAdmin, CancellationToken cancellationToken = default);
    Task ReorderStepsAsync(Guid templateId, Guid companyId, bool isAdmin, ReorderStepsRequest request, CancellationToken cancellationToken = default);
}
