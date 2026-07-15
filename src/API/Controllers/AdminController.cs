using Application.DTOs.Companies;
using Application.DTOs.Users;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;
    private readonly IValidator<CreateCompanyRequest> _companyValidator;

    public AdminController(
        ICompanyService companyService,
        IUserService userService,
        INotificationService notificationService,
        IValidator<CreateCompanyRequest> companyValidator)
    {
        _companyService = companyService;
        _userService = userService;
        _notificationService = notificationService;
        _companyValidator = companyValidator;
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
    {
        var companies = await _companyService.GetAllAsync(cancellationToken);
        return Ok(companies);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var validation = await _companyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var company = await _companyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCompanies), new { }, company);
    }

    [HttpGet("companies/{companyId:guid}/users")]
    public async Task<IActionResult> GetCompanyUsers(Guid companyId, CancellationToken cancellationToken)
    {
        var users = await _userService.GetUsersAsync(companyId, cancellationToken);
        return Ok(users);
    }

    [HttpPost("companies/{companyId:guid}/users")]
    public async Task<IActionResult> CreateCompanyUser(Guid companyId, [FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, out _))
        {
            return BadRequest(new { errors = new[] { $"Invalid role. Valid roles: {string.Join(", ", Enum.GetNames<UserRole>())}" } });
        }

        var user = await _userService.CreateUserAsync(companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCompanyUsers), new { companyId }, user);
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (request.Role is not null && !Enum.TryParse<UserRole>(request.Role, out _))
        {
            return BadRequest(new { errors = new[] { $"Invalid role. Valid roles: {string.Join(", ", Enum.GetNames<UserRole>())}" } });
        }

        var user = await _userService.UpdateUserAsync(userId, null, request, cancellationToken);
        return Ok(user);
    }

    [HttpPost("maintenance/cleanup-notifications")]
    public async Task<IActionResult> CleanupNotifications([FromQuery] int olderThanDays = 30, CancellationToken cancellationToken = default)
    {
        if (olderThanDays < 0)
        {
            return BadRequest(new { errors = new[] { "olderThanDays must be 0 or greater." } });
        }

        var deleted = await _notificationService.DeleteOlderThanAsync(olderThanDays, cancellationToken);
        return Ok(new { deleted });
    }
}
