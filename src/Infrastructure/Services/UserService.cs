using Application.DTOs;
using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly JwtService _jwtService;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(UserManager<User> userManager, JwtService jwtService, IMapper mapper, ILogger<UserService> logger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<UserDto>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .Where(u => u.CompanyId == companyId)
            .Include(u => u.Company)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<UserDto>>(users);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .Include(u => u.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> CreateUserAsync(Guid companyId, CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var role = Enum.Parse<UserRole>(request.Role);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyId = companyId,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"User creation failed: {errors}");
        }

        _logger.LogInformation("User {Email} created in company {CompanyId}", user.Email, companyId);

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateUserAsync(Guid userId, Guid? companyId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var query = _userManager.Users.AsQueryable();
        User? user;
        if (companyId.HasValue)
            user = await query.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        else
            user = await query.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        if (request.FirstName is not null)
            user.FirstName = request.FirstName;

        if (request.LastName is not null)
            user.LastName = request.LastName;

        if (request.Role is not null)
            user.Role = Enum.Parse<UserRole>(request.Role);

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"User update failed: {errors}");
        }

        // Revoke all refresh tokens when user is deactivated
        if (request.IsActive == false)
        {
            await _jwtService.RevokeAllUserTokensAsync(userId, cancellationToken);
        }

        _logger.LogInformation("User {UserId} updated", userId);


        return _mapper.Map<UserDto>(user);
    }

    public async Task DeleteUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException("User not found.");

        // Revoke all refresh tokens before deleting
        await _jwtService.RevokeAllUserTokensAsync(userId, cancellationToken);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"User deletion failed: {errors}");
        }

        _logger.LogInformation("User {UserId} permanently deleted from company {CompanyId}", userId, companyId);
    }
}
