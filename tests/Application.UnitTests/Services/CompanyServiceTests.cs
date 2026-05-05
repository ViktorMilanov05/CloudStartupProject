using Application.DTOs.Companies;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests.Services;

public class CompanyServiceTests
{
    private readonly Mock<ILogger<CompanyService>> _loggerMock = new();

    private (CompanyService service, AppDbContext db, Mock<UserManager<User>> userManagerMock) CreateService(AppDbContext? existingDb = null)
    {
        var db = existingDb ?? TestHelpers.CreateInMemoryDbContext();
        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.Users).Returns(db.Users);

        var service = new CompanyService(db, userManager.Object, _loggerMock.Object);
        return (service, db, userManager);
    }

    // ── GetAllAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllCompanies()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var c1 = new Company { Id = Guid.NewGuid(), Name = "Bravo Corp", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var c2 = new Company { Id = Guid.NewGuid(), Name = "Alpha Inc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Companies.AddRange(c1, c2);

        // Add a user to c1
        db.Users.Add(TestHelpers.CreateTestUser(c1.Id));
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);
        var result = await service.GetAllAsync();

        result.Should().HaveCount(2);
        // Ordered by name
        result[0].Name.Should().Be("Alpha Inc");
        result[1].Name.Should().Be("Bravo Corp");
        result[1].UserCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyWhenNoCompanies()
    {
        var (service, _, _) = CreateService();
        var result = await service.GetAllAsync();
        result.Should().BeEmpty();
    }

    // ── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Success_ReturnsCompanyWithManager()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var (service, _, umMock) = CreateService(db);

        umMock.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<User, string>((u, _) =>
            {
                db.Users.Add(u);
                db.SaveChanges();
            });

        var result = await service.CreateAsync(new CreateCompanyRequest
        {
            CompanyName = "New Corp",
            ManagerEmail = "mgr@new.com",
            ManagerPassword = "Pass123!",
            ManagerFirstName = "Manager",
            ManagerLastName = "One"
        });

        result.Name.Should().Be("New Corp");
        result.UserCount.Should().Be(1);

        // Company should exist in DB
        var company = await db.Companies.FirstAsync(c => c.Name == "New Corp");
        company.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_ManagerCreationFails_RollsBackCompany()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var (service, _, umMock) = CreateService(db);

        umMock.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Weak password" }));

        var act = () => service.CreateAsync(new CreateCompanyRequest
        {
            CompanyName = "Fail Corp",
            ManagerEmail = "mgr@fail.com",
            ManagerPassword = "x",
            ManagerFirstName = "Fail",
            ManagerLastName = "Manager"
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Manager creation failed*Weak password*");

        // Company should have been rolled back
        var companyExists = await db.Companies.AnyAsync(c => c.Name == "Fail Corp");
        companyExists.Should().BeFalse();
    }
}
