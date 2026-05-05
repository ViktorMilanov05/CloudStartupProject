using Application.DTOs.Auth;
using AutoMapper;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly IMapper _mapper;

    public AuthServiceTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    private (AuthService service, AppDbContext db, Mock<UserManager<User>> userManagerMock) CreateService(AppDbContext? existingDb = null)
    {
        var db = existingDb ?? TestHelpers.CreateInMemoryDbContext();
        var jwtConfig = TestHelpers.CreateTestConfiguration();
        var jwtService = new JwtService(jwtConfig, db);

        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.Users).Returns(db.Users);
        userManager.Setup(m => m.NormalizeEmail(It.IsAny<string>()))
            .Returns<string>(e => e?.ToUpperInvariant());

        var service = new AuthService(userManager.Object, jwtService, db, _mapper, _loggerMock.Object);
        return (service, db, userManager);
    }

    // ── LoginAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var company = TestHelpers.CreateTestCompany();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "login@test.com",
            Email = "login@test.com",
            NormalizedEmail = "LOGIN@TEST.COM",
            FirstName = "Login",
            LastName = "User",
            CompanyId = company.Id,
            Company = company,
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (service, _, umMock) = CreateService(db);
        umMock.Setup(m => m.CheckPasswordAsync(It.IsAny<User>(), "password123"))
            .ReturnsAsync(true);

        var result = await service.LoginAsync(new LoginRequest { Email = "login@test.com", Password = "password123" });

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("login@test.com");
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_Throws()
    {
        var (service, _, _) = CreateService();

        var act = () => service.LoginAsync(new LoginRequest { Email = "noone@test.com", Password = "password" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_Throws()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var user = TestHelpers.CreateTestUser(Guid.NewGuid());
        user.Email = "inactive@test.com";
        user.NormalizedEmail = "INACTIVE@TEST.COM";
        user.IsActive = false;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);

        var act = () => service.LoginAsync(new LoginRequest { Email = "inactive@test.com", Password = "x" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Throws()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var company = TestHelpers.CreateTestCompany();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "wrong@test.com",
            Email = "wrong@test.com",
            NormalizedEmail = "WRONG@TEST.COM",
            FirstName = "Wrong",
            LastName = "Pass",
            CompanyId = company.Id,
            Company = company,
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (service, _, umMock) = CreateService(db);
        umMock.Setup(m => m.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var act = () => service.LoginAsync(new LoginRequest { Email = "wrong@test.com", Password = "bad" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Invalid email or password*");
    }

    // ── RefreshTokenAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var company = TestHelpers.CreateTestCompany();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "refresh@test.com",
            Email = "refresh@test.com",
            NormalizedEmail = "REFRESH@TEST.COM",
            FirstName = "Refresh",
            LastName = "User",
            CompanyId = company.Id,
            Company = company,
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.Users.Add(user);

        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "old-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);

        var result = await service.RefreshTokenAsync("old-refresh-token");

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe("old-refresh-token");
        result.User.Email.Should().Be("refresh@test.com");

        // Old token should be revoked
        var oldToken = await db.RefreshTokens.FirstAsync(t => t.Token == "old-refresh-token");
        oldToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_Throws()
    {
        var (service, _, _) = CreateService();

        var act = () => service.RefreshTokenAsync("nonexistent-token");
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Invalid or expired refresh token*");
    }

    [Fact]
    public async Task RefreshTokenAsync_DeactivatedUser_Throws()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var company = TestHelpers.CreateTestCompany();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "deact@test.com",
            Email = "deact@test.com",
            NormalizedEmail = "DEACT@TEST.COM",
            FirstName = "Deact",
            LastName = "User",
            CompanyId = company.Id,
            Company = company,
            Role = UserRole.User,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.Users.Add(user);

        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "deact-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);

        var act = () => service.RefreshTokenAsync("deact-token");
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*User account is deactivated*");
    }

    // ── RevokeRefreshTokenAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RevokeRefreshTokenAsync_RevokesToken()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var user = TestHelpers.CreateTestUser(Guid.NewGuid());
        db.Users.Add(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "revoke-me",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);
        await service.RevokeRefreshTokenAsync("revoke-me");

        var token = await db.RefreshTokens.FirstAsync(t => t.Token == "revoke-me");
        token.IsRevoked.Should().BeTrue();
    }

    // ── IsSetupRequiredAsync ────────────────────────────────────────────────

    [Fact]
    public async Task IsSetupRequiredAsync_ReturnsTrueWhenNoAdmin()
    {
        var (service, _, _) = CreateService();
        var result = await service.IsSetupRequiredAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_ReturnsFalseWhenAdminExists()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        db.Users.Add(TestHelpers.CreateTestAdmin());
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);
        var result = await service.IsSetupRequiredAsync();
        result.Should().BeFalse();
    }

    // ── SetupAdminAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetupAdminAsync_WhenAdminExists_Throws()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        db.Users.Add(TestHelpers.CreateTestAdmin());
        await db.SaveChangesAsync();

        var (service, _, _) = CreateService(db);

        var act = () => service.SetupAdminAsync(new SetupRequest
        {
            Email = "admin2@test.com",
            Password = "Password123!",
            FirstName = "New",
            LastName = "Admin"
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Setup has already been completed*");
    }

    [Fact]
    public async Task SetupAdminAsync_Success_ReturnsTokens()
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

        var result = await service.SetupAdminAsync(new SetupRequest
        {
            Email = "admin@test.com",
            Password = "Password123!",
            FirstName = "System",
            LastName = "Admin"
        });

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("admin@test.com");
        result.User.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task SetupAdminAsync_PasswordFails_Throws()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var (service, _, umMock) = CreateService(db);

        umMock.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var act = () => service.SetupAdminAsync(new SetupRequest
        {
            Email = "admin@test.com",
            Password = "weak",
            FirstName = "System",
            LastName = "Admin"
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Failed to create admin account*Password too weak*");
    }
}
