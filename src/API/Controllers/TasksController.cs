using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.DTOs.Tasks;
using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager,User")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<CreateTaskFromTemplateRequest> _createFromTemplateValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<CreateTaskStepRequest> _createStepValidator;
    private readonly IValidator<UpdateTaskStepRequest> _updateStepValidator;
    private readonly IValidator<ReorderTaskStepsRequest> _reorderValidator;
    private readonly IValidator<CreateTaskCommentRequest> _createCommentValidator;
    private readonly IValidator<UpdateTaskCommentRequest> _updateCommentValidator;

    public TasksController(
        ITaskService taskService,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<CreateTaskFromTemplateRequest> createFromTemplateValidator,
        IValidator<UpdateTaskRequest> updateValidator,
        IValidator<CreateTaskStepRequest> createStepValidator,
        IValidator<UpdateTaskStepRequest> updateStepValidator,
        IValidator<ReorderTaskStepsRequest> reorderValidator,
        IValidator<CreateTaskCommentRequest> createCommentValidator,
        IValidator<UpdateTaskCommentRequest> updateCommentValidator)
    {
        _taskService = taskService;
        _createValidator = createValidator;
        _createFromTemplateValidator = createFromTemplateValidator;
        _updateValidator = updateValidator;
        _createStepValidator = createStepValidator;
        _updateStepValidator = updateStepValidator;
        _reorderValidator = reorderValidator;
        _createCommentValidator = createCommentValidator;
        _updateCommentValidator = updateCommentValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TaskFilterRequest filter, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        var result = await _taskService.GetAllAsync(userId, companyId, role, filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        var task = await _taskService.GetByIdAsync(id, userId, companyId, role, cancellationToken);
        if (task is null)
            return NotFound();
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var task = await _taskService.CreateAsync(userId, companyId, role, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPost("from-template/{templateId:guid}")]
    public async Task<IActionResult> CreateFromTemplate(Guid templateId, [FromBody] CreateTaskFromTemplateRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createFromTemplateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var task = await _taskService.CreateFromTemplateAsync(templateId, userId, companyId, role, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var task = await _taskService.UpdateAsync(id, userId, companyId, role, request, cancellationToken);
        return Ok(task);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (userId, companyId, _) = GetCallerContext();
        await _taskService.DeleteAsync(id, userId, companyId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/steps")]
    public async Task<IActionResult> AddStep(Guid id, [FromBody] CreateTaskStepRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createStepValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var step = await _taskService.AddStepAsync(id, userId, companyId, role, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, step);
    }

    [HttpPut("{id:guid}/steps/{stepId:guid}")]
    public async Task<IActionResult> UpdateStep(Guid id, Guid stepId, [FromBody] UpdateTaskStepRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateStepValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var step = await _taskService.UpdateStepAsync(id, stepId, userId, companyId, role, request, cancellationToken);
        return Ok(step);
    }

    [HttpPut("{id:guid}/steps/{stepId:guid}/complete")]
    public async Task<IActionResult> CompleteStep(Guid id, Guid stepId, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        var newStatus = await _taskService.CompleteStepAsync(id, stepId, userId, companyId, role, cancellationToken);
        return Ok(new { newStatus });
    }

    [HttpPut("{id:guid}/steps/{stepId:guid}/uncomplete")]
    public async Task<IActionResult> UncompleteStep(Guid id, Guid stepId, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        var newStatus = await _taskService.UncompleteStepAsync(id, stepId, userId, companyId, role, cancellationToken);
        return Ok(new { newStatus });
    }

    [HttpDelete("{id:guid}/steps/{stepId:guid}")]
    public async Task<IActionResult> DeleteStep(Guid id, Guid stepId, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        await _taskService.DeleteStepAsync(id, stepId, userId, companyId, role, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/steps/reorder")]
    public async Task<IActionResult> ReorderSteps(Guid id, [FromBody] ReorderTaskStepsRequest request, CancellationToken cancellationToken)
    {
        var validation = await _reorderValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        await _taskService.ReorderStepsAsync(id, userId, companyId, role, request, cancellationToken);
        return NoContent();
    }

    // ── Comments ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        var comments = await _taskService.GetCommentsAsync(id, userId, companyId, role, cancellationToken);
        return Ok(comments);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createCommentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var comment = await _taskService.AddCommentAsync(id, userId, companyId, role, request, cancellationToken);
        return CreatedAtAction(nameof(GetComments), new { id }, comment);
    }

    [HttpPut("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(Guid id, Guid commentId, [FromBody] UpdateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateCommentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var (userId, companyId, role) = GetCallerContext();
        var comment = await _taskService.UpdateCommentAsync(id, commentId, userId, companyId, role, request, cancellationToken);
        return Ok(comment);
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();
        await _taskService.DeleteCommentAsync(id, commentId, userId, companyId, role, cancellationToken);
        return NoContent();
    }

    private (Guid UserId, Guid CompanyId, string Role) GetCallerContext()
    {
        var userIdStr = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        var companyIdStr = User.FindFirstValue("companyId");
        var role = User.FindFirstValue("role")
            ?? throw new UnauthorizedAccessException("Role claim not found.");

        var companyId = string.IsNullOrEmpty(companyIdStr) ? Guid.Empty : Guid.Parse(companyIdStr);
        return (Guid.Parse(userIdStr), companyId, role);
    }
}
