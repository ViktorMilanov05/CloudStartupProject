using Application.DTOs.Templates;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class TemplateService : ITemplateService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(AppDbContext dbContext, ILogger<TemplateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<TemplateDto>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Templates
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        return await query
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                CreatedById = t.CreatedById,
                CreatedByName = t.CreatedBy.FirstName + " " + t.CreatedBy.LastName,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                StepCount = t.Steps.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TemplateDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.Templates
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null)
            return null;

        return MapToDetailDto(template);
    }

    public async Task<TemplateDetailDto> CreateAsync(Guid createdById, CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedById = createdById,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < request.Steps.Count; i++)
        {
            template.Steps.Add(new TemplateStep
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Title = request.Steps[i].Title,
                Instructions = request.Steps[i].Instructions,
                SortOrder = i
            });
        }

        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template '{TemplateName}' created by {UserId}", template.Name, createdById);

        // Reload with navigation properties
        var created = await _dbContext.Templates
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
            .FirstAsync(t => t.Id == template.Id, cancellationToken);

        return MapToDetailDto(created);
    }

    public async Task<TemplateDetailDto> UpdateAsync(Guid id, UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.Templates
            .Include(t => t.CreatedBy)
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null)
            throw new KeyNotFoundException("Template not found.");

        if (request.Name is not null)
            template.Name = request.Name;

        if (request.Description is not null)
            template.Description = request.Description;

        if (request.IsActive.HasValue)
            template.IsActive = request.IsActive.Value;

        template.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template '{TemplateId}' updated", id);

        return MapToDetailDto(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.Templates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (template is null)
            throw new KeyNotFoundException("Template not found.");

        _dbContext.Templates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template '{TemplateId}' permanently deleted", id);
    }

    public async Task<TemplateStepDto> AddStepAsync(Guid templateId, CreateTemplateStepRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.Templates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template is null)
            throw new KeyNotFoundException("Template not found.");

        var maxSortOrder = template.Steps.Any() ? template.Steps.Max(s => s.SortOrder) : -1;

        var step = new TemplateStep
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            Title = request.Title,
            Instructions = request.Instructions,
            SortOrder = maxSortOrder + 1
        };

        _dbContext.TemplateSteps.Add(step);

        await _dbContext.Templates
            .Where(t => t.Id == templateId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToStepDto(step);
    }

    public async Task<TemplateStepDto> UpdateStepAsync(Guid templateId, Guid stepId, UpdateTemplateStepRequest request, CancellationToken cancellationToken = default)
    {
        var step = await _dbContext.TemplateSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.TemplateId == templateId, cancellationToken);

        if (step is null)
            throw new KeyNotFoundException("Template step not found.");

        if (request.Title is not null)
            step.Title = request.Title;

        if (request.Instructions is not null)
            step.Instructions = request.Instructions;

        // Update parent template timestamp
        await _dbContext.Templates
            .Where(t => t.Id == templateId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToStepDto(step);
    }

    public async Task DeleteStepAsync(Guid templateId, Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = await _dbContext.TemplateSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.TemplateId == templateId, cancellationToken);

        if (step is null)
            throw new KeyNotFoundException("Template step not found.");

        _dbContext.TemplateSteps.Remove(step);

        // Re-order remaining steps
        var remainingSteps = await _dbContext.TemplateSteps
            .Where(s => s.TemplateId == templateId && s.Id != stepId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < remainingSteps.Count; i++)
        {
            remainingSteps[i].SortOrder = i;
        }

        // Update parent template timestamp
        await _dbContext.Templates
            .Where(t => t.Id == templateId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderStepsAsync(Guid templateId, ReorderStepsRequest request, CancellationToken cancellationToken = default)
    {
        var steps = await _dbContext.TemplateSteps
            .Where(s => s.TemplateId == templateId)
            .ToListAsync(cancellationToken);

        if (steps.Count != request.StepIds.Count || !steps.All(s => request.StepIds.Contains(s.Id)))
            throw new InvalidOperationException("Step IDs do not match existing steps for this template.");

        for (var i = 0; i < request.StepIds.Count; i++)
        {
            var step = steps.First(s => s.Id == request.StepIds[i]);
            step.SortOrder = i;
        }

        // Update parent template timestamp
        await _dbContext.Templates
            .Where(t => t.Id == templateId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TemplateDetailDto MapToDetailDto(Template template) => new()
    {
        Id = template.Id,
        Name = template.Name,
        Description = template.Description,
        CreatedById = template.CreatedById,
        CreatedByName = template.CreatedBy.FirstName + " " + template.CreatedBy.LastName,
        IsActive = template.IsActive,
        CreatedAt = template.CreatedAt,
        UpdatedAt = template.UpdatedAt,
        Steps = template.Steps.OrderBy(s => s.SortOrder).Select(MapToStepDto).ToList()
    };

    private static TemplateStepDto MapToStepDto(TemplateStep step) => new()
    {
        Id = step.Id,
        TemplateId = step.TemplateId,
        Title = step.Title,
        Instructions = step.Instructions,
        SortOrder = step.SortOrder
    };
}
