using Application.DTOs.Auth;
using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class SetupController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<SetupRequest> _setupValidator;
    private readonly IConfiguration _configuration;

    public SetupController(
        IAuthService authService,
        IValidator<SetupRequest> setupValidator,
        IConfiguration configuration)
    {
        _authService = authService;
        _setupValidator = setupValidator;
        _configuration = configuration;
    }

    /// <summary>
    /// Check if initial setup is required (no admin exists yet).
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var setupRequired = await _authService.IsSetupRequiredAsync(cancellationToken);
        return Ok(new { setupRequired });
    }

    /// <summary>
    /// Create the initial admin account. Only works when no admin exists.
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] SetupRequest request, CancellationToken cancellationToken)
    {
        var setupRequired = await _authService.IsSetupRequiredAsync(cancellationToken);
        if (!setupRequired)
        {
            return NotFound();
        }

        var validation = await _setupValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var result = await _authService.SetupAdminAsync(request, cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var refreshTokenDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");

        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(refreshTokenDays)
        });
    }
}
