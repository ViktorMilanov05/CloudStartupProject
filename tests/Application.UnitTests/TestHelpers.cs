using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests;

public static class TestHelpers
{
    public static AppDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    public static Company CreateTestCompany(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test Company",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static User CreateTestUser(Guid companyId, UserRole role = UserRole.User, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserName = $"user-{Guid.NewGuid():N}@test.com",
        Email = $"user-{Guid.NewGuid():N}@test.com",
        NormalizedEmail = $"USER-{Guid.NewGuid():N}@TEST.COM",
        FirstName = "Test",
        LastName = "User",
        CompanyId = companyId,
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public static User CreateTestAdmin(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserName = "admin@test.com",
        Email = "admin@test.com",
        NormalizedEmail = "ADMIN@TEST.COM",
        FirstName = "System",
        LastName = "Admin",
        CompanyId = null,
        Role = UserRole.Admin,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public static Template CreateTestTemplate(Guid createdById, int stepCount = 3) 
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Test Template",
            Description = "A test template",
            CreatedById = createdById,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        for (int i = 0; i < stepCount; i++)
        {
            template.Steps.Add(new TemplateStep
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Title = $"Step {i + 1}",
                Instructions = $"Instructions for step {i + 1}",
                SortOrder = i
            });
        }

        return template;
    }

    public static IConfiguration CreateTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-super-secret-key-that-is-at-least-32-bytes!!",
                ["Jwt:Issuer"] = "TestApp",
                ["Jwt:Audience"] = "TestApp",
                ["Jwt:AccessTokenExpirationMinutes"] = "30",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            })
            .Build();
}
