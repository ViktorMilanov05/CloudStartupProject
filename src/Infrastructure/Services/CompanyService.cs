using Application.DTOs.Companies;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CompanyService : ICompanyService
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(AppDbContext dbContext, UserManager<User> userManager, ILogger<CompanyService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<List<CompanyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Companies
            .AsNoTracking()
            .Select(c => new CompanyDto
            {
                Id = c.Id,
                Name = c.Name,
                CreatedAt = c.CreatedAt,
                UserCount = c.Users.Count
            })
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.CompanyName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Companies.Add(company);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var manager = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.ManagerEmail,
            Email = request.ManagerEmail,
            FirstName = request.ManagerFirstName,
            LastName = request.ManagerLastName,
            CompanyId = company.Id,
            Role = UserRole.Manager,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(manager, request.ManagerPassword);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Manager creation failed: {errors}");
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Company '{CompanyName}' created with manager {Email}", company.Name, manager.Email);

        return new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            CreatedAt = company.CreatedAt,
            UserCount = 1
        };
    }
}
