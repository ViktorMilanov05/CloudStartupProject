using Application.DTOs;
using Application.DTOs.Auth;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly JwtService _jwtService;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        JwtService jwtService,
        AppDbContext dbContext,
        IMapper mapper,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _dbContext = dbContext;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = _userManager.NormalizeEmail(request.Email);
        var user = await _userManager.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user.Id, cancellationToken);

        _logger.LogInformation("User {Email} logged in", user.Email);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            User = _mapper.Map<UserDto>(user)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var storedToken = await _jwtService.ValidateRefreshTokenAsync(refreshToken, cancellationToken);
        if (storedToken is null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var user = storedToken.User;
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is deactivated.");
        }

        // Generate new tokens first, then revoke old one (avoids race condition
        // where a parallel request finds the old token revoked before the new one exists)
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = await _jwtService.GenerateRefreshTokenAsync(user.Id, cancellationToken);

        storedToken.IsRevoked = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken.Token,
            User = _mapper.Map<UserDto>(user)
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _jwtService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
    }

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        return !await _userManager.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken);
    }

    public async Task<AuthResponse> SetupAdminAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        var adminExists = await _userManager.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken);
        if (adminExists)
        {
            throw new InvalidOperationException("Setup has already been completed.");
        }

        var admin = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyId = null,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(admin, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create admin account: {errors}");
        }

        _logger.LogInformation("Admin user created via first-run setup: {Email}", admin.Email);

        var accessToken = _jwtService.GenerateAccessToken(admin);
        var refreshToken = await _jwtService.GenerateRefreshTokenAsync(admin.Id, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            User = _mapper.Map<UserDto>(admin)
        };
    }
}
