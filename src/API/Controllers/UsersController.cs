using System.Security.Claims;
using Application.DTOs.Users;
using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;

    public UsersController(
        IUserService userService,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator)
    {
        _userService = userService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId();
        var users = await _userService.GetUsersAsync(companyId, cancellationToken);
        return Ok(users);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _userService.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var companyId = GetCompanyId();
        var user = await _userService.CreateUserAsync(companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCurrentUser), new { }, user);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var companyId = GetCompanyId();
        var user = await _userService.UpdateUserAsync(id, companyId, request, cancellationToken);
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId();
        await _userService.DeleteUserAsync(id, companyId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(sub);
    }

    private Guid GetCompanyId()
    {
        var companyId = User.FindFirstValue("companyId")
            ?? throw new UnauthorizedAccessException("Company ID claim not found.");
        return Guid.Parse(companyId);
    }
}
