using Application.DTOs.Auth;
using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginValidator,
        IConfiguration configuration)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "Refresh token not found." });
        }

        var result = await _authService.RefreshTokenAsync(refreshToken, cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }

        Response.Cookies.Delete("refreshToken");
        return NoContent();
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
