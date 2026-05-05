using Application.DTOs.Templates;

namespace Application.Interfaces;

public interface ITemplateService
{
    Task<List<TemplateDto>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<TemplateDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TemplateDetailDto> CreateAsync(Guid createdById, CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TemplateDetailDto> UpdateAsync(Guid id, UpdateTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TemplateStepDto> AddStepAsync(Guid templateId, CreateTemplateStepRequest request, CancellationToken cancellationToken = default);
    Task<TemplateStepDto> UpdateStepAsync(Guid templateId, Guid stepId, UpdateTemplateStepRequest request, CancellationToken cancellationToken = default);
    Task DeleteStepAsync(Guid templateId, Guid stepId, CancellationToken cancellationToken = default);
    Task ReorderStepsAsync(Guid templateId, ReorderStepsRequest request, CancellationToken cancellationToken = default);
}
