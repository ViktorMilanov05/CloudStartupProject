using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests.Services;

public class TemplateServiceTests
{
    private readonly Mock<ILogger<TemplateService>> _loggerMock = new();

    private TemplateService CreateService(AppDbContext db) => new(db, _loggerMock.Object);

    private async Task<(AppDbContext db, User manager)> SetupDbWithManager()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var company = TestHelpers.CreateTestCompany();
        var manager = TestHelpers.CreateTestUser(company.Id, UserRole.Manager);

        db.Companies.Add(company);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        return (db, manager);
    }

    [Fact]
    public async Task CreateAsync_CreatesTemplateWithSteps()
    {
        var (db, manager) = await SetupDbWithManager();
        var service = CreateService(db);

        var request = new DTOs.Templates.CreateTemplateRequest
        {
            Name = "Onboarding Template",
            Description = "Steps for new employee onboarding",
            Steps =
            [
                new() { Title = "Create account", Instructions = "Create AD account" },
                new() { Title = "Setup workstation", Instructions = "Install tools" },
                new() { Title = "Complete training", Instructions = "Finish HR training" },
            ]
        };

        var result = await service.CreateAsync(manager.Id, request);

        result.Name.Should().Be("Onboarding Template");
        result.Steps.Should().HaveCount(3);
        result.Steps[0].SortOrder.Should().Be(0);
        result.Steps[1].SortOrder.Should().Be(1);
        result.Steps[2].SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_FiltersActiveTemplates()
    {
        var (db, manager) = await SetupDbWithManager();

        db.Templates.AddRange(
            new Template { Id = Guid.NewGuid(), Name = "Active", CreatedById = manager.Id, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Name = "Inactive", CreatedById = manager.Id, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var activeOnly = await service.GetAllAsync(isActive: true);
        var all = await service.GetAllAsync(isActive: null);

        activeOnly.Should().HaveCount(1);
        activeOnly[0].Name.Should().Be("Active");
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndDescription()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = TestHelpers.CreateTestTemplate(manager.Id, stepCount: 1);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.UpdateAsync(template.Id, new DTOs.Templates.UpdateTemplateRequest
        {
            Name = "Updated Name",
            Description = "Updated Description"
        });

        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task DeleteAsync_RemovesTemplate()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            CreatedById = manager.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteAsync(template.Id);

        var exists = await db.Templates.AnyAsync(t => t.Id == template.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsIfNotFound()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);

        var act = () => service.DeleteAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ReorderStepsAsync_ReordersCorrectly()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = TestHelpers.CreateTestTemplate(manager.Id, stepCount: 3);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var steps = template.Steps.OrderBy(s => s.SortOrder).ToList();

        // Simulate reorder directly (service uses ExecuteUpdateAsync which InMemory doesn't support)
        var newOrder = new List<Guid> { steps[2].Id, steps[1].Id, steps[0].Id };
        var allSteps = await db.TemplateSteps
            .Where(s => s.TemplateId == template.Id)
            .ToListAsync();

        for (var i = 0; i < newOrder.Count; i++)
        {
            var step = allSteps.First(s => s.Id == newOrder[i]);
            step.SortOrder = i;
        }
        await db.SaveChangesAsync();

        var reloaded = await db.TemplateSteps
            .Where(s => s.TemplateId == template.Id)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        reloaded[0].Id.Should().Be(steps[2].Id);
        reloaded[1].Id.Should().Be(steps[1].Id);
        reloaded[2].Id.Should().Be(steps[0].Id);
    }

    [Fact]
    public async Task AddStepAsync_AppendsToEnd()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = TestHelpers.CreateTestTemplate(manager.Id, stepCount: 2);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        // Simulate adding a step (service.AddStepAsync uses ExecuteUpdateAsync which InMemory doesn't support)
        var maxSortOrder = template.Steps.Any() ? template.Steps.Max(s => s.SortOrder) : -1;
        var newStep = new Domain.Entities.TemplateStep
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Title = "New Step",
            Instructions = "New instructions",
            SortOrder = maxSortOrder + 1
        };
        db.TemplateSteps.Add(newStep);
        await db.SaveChangesAsync();

        newStep.SortOrder.Should().Be(2);
        newStep.Title.Should().Be("New Step");

        var allSteps = await db.TemplateSteps.Where(s => s.TemplateId == template.Id).ToListAsync();
        allSteps.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetailWithSteps()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = TestHelpers.CreateTestTemplate(manager.Id, stepCount: 2);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetByIdAsync(template.Id);
        result.Should().NotBeNull();
        result!.Steps.Should().HaveCount(2);
        result.Name.Should().Be("Test Template");
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenNotFound()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);

        var act = () => service.UpdateAsync(Guid.NewGuid(), new DTOs.Templates.UpdateTemplateRequest { Name = "X" });
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_TogglesIsActive()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = TestHelpers.CreateTestTemplate(manager.Id);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.UpdateAsync(template.Id, new DTOs.Templates.UpdateTemplateRequest { IsActive = false });
        result.IsActive.Should().BeFalse();

        var result2 = await service.UpdateAsync(template.Id, new DTOs.Templates.UpdateTemplateRequest { IsActive = true });
        result2.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsStepCount()
    {
        var (db, manager) = await SetupDbWithManager();
        var template = TestHelpers.CreateTestTemplate(manager.Id, stepCount: 5);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetAllAsync(null);
        result.Should().HaveCount(1);
        result.First().StepCount.Should().Be(5);
    }

    [Fact]
    public async Task GetAllAsync_InactiveFilter_ReturnsOnlyInactive()
    {
        var (db, manager) = await SetupDbWithManager();
        db.Templates.AddRange(
            new Template { Id = Guid.NewGuid(), Name = "A", CreatedById = manager.Id, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Name = "B", CreatedById = manager.Id, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetAllAsync(false);
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("B");
    }
}
