using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.DTOs.Templates;
using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly IValidator<CreateTemplateRequest> _createValidator;
    private readonly IValidator<UpdateTemplateRequest> _updateValidator;
    private readonly IValidator<CreateTemplateStepRequest> _createStepValidator;
    private readonly IValidator<UpdateTemplateStepRequest> _updateStepValidator;
    private readonly IValidator<ReorderStepsRequest> _reorderValidator;

    public TemplatesController(
        ITemplateService templateService,
        IValidator<CreateTemplateRequest> createValidator,
        IValidator<UpdateTemplateRequest> updateValidator,
        IValidator<CreateTemplateStepRequest> createStepValidator,
        IValidator<UpdateTemplateStepRequest> updateStepValidator,
        IValidator<ReorderStepsRequest> reorderValidator)
    {
        _templateService = templateService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createStepValidator = createStepValidator;
        _updateStepValidator = updateStepValidator;
        _reorderValidator = reorderValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var (companyId, isAdmin) = GetCompanyContext();
        var templates = await _templateService.GetAllAsync(companyId, isAdmin, isActive, cancellationToken);
        return Ok(templates);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (companyId, isAdmin) = GetCompanyContext();
        var template = await _templateService.GetByIdAsync(id, companyId, isAdmin, cancellationToken);
        if (template is null)
            return NotFound();

        return Ok(template);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var userId = GetUserId();
        var (companyId, _) = GetCompanyContext();
        var template = await _templateService.CreateAsync(userId, companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (companyId, isAdmin) = GetCompanyContext();
        var template = await _templateService.UpdateAsync(id, companyId, isAdmin, request, cancellationToken);
        return Ok(template);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (companyId, isAdmin) = GetCompanyContext();
        await _templateService.DeleteAsync(id, companyId, isAdmin, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/steps")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddStep(Guid id, [FromBody] CreateTemplateStepRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createStepValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (companyId, isAdmin) = GetCompanyContext();
        var step = await _templateService.AddStepAsync(id, companyId, isAdmin, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, step);
    }

    [HttpPut("{id:guid}/steps/{stepId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateStep(Guid id, Guid stepId, [FromBody] UpdateTemplateStepRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateStepValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (companyId, isAdmin) = GetCompanyContext();
        var step = await _templateService.UpdateStepAsync(id, stepId, companyId, isAdmin, request, cancellationToken);
        return Ok(step);
    }

    [HttpDelete("{id:guid}/steps/{stepId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteStep(Guid id, Guid stepId, CancellationToken cancellationToken)
    {
        var (companyId, isAdmin) = GetCompanyContext();
        await _templateService.DeleteStepAsync(id, stepId, companyId, isAdmin, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/steps/reorder")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ReorderSteps(Guid id, [FromBody] ReorderStepsRequest request, CancellationToken cancellationToken)
    {
        var validation = await _reorderValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (companyId, isAdmin) = GetCompanyContext();
        await _templateService.ReorderStepsAsync(id, companyId, isAdmin, request, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(sub);
    }

    private (Guid CompanyId, bool IsAdmin) GetCompanyContext()
    {
        var companyIdStr = User.FindFirstValue("companyId");
        var companyId = string.IsNullOrEmpty(companyIdStr) ? Guid.Empty : Guid.Parse(companyIdStr);
        var isAdmin = string.Equals(User.FindFirstValue("role"), "Admin", StringComparison.OrdinalIgnoreCase);
        return (companyId, isAdmin);
    }
}
