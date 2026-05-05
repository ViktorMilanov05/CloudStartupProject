using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Application.UnitTests.Services;

public class JwtServiceTests
{
    private JwtService CreateService(Infrastructure.Data.AppDbContext db)
    {
        var config = TestHelpers.CreateTestConfiguration();
        return new JwtService(config, db);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);
        var user = TestHelpers.CreateTestUser(Guid.NewGuid(), UserRole.Manager);

        var token = service.GenerateAccessToken(user);

        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == "firstName" && c.Value == user.FirstName);
        jwt.Claims.Should().Contain(c => c.Type == "lastName" && c.Value == user.LastName);
    }

    [Fact]
    public void GenerateAccessToken_IncludesRoleClaim()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);
        var user = TestHelpers.CreateTestUser(Guid.NewGuid(), UserRole.Admin);
        user.CompanyId = null;

        var token = service.GenerateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateAccessToken_CompanyIdClaimIsEmptyForNullCompany()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);
        var user = TestHelpers.CreateTestAdmin();

        var token = service.GenerateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "companyId" && c.Value == "");
    }

    [Fact]
    public void GenerateAccessToken_CompanyIdClaimIsSetForCompanyUser()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);
        var companyId = Guid.NewGuid();
        var user = TestHelpers.CreateTestUser(companyId, UserRole.User);

        var token = service.GenerateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "companyId" && c.Value == companyId.ToString());
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_PersistsToken()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var refreshToken = await service.GenerateRefreshTokenAsync(userId);

        refreshToken.Token.Should().NotBeNullOrEmpty();
        refreshToken.UserId.Should().Be(userId);
        refreshToken.IsRevoked.Should().BeFalse();
        refreshToken.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var stored = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == refreshToken.Id);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsTokenWhenValid()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var company = TestHelpers.CreateTestCompany();
        var user = TestHelpers.CreateTestUser(company.Id);
        db.Companies.Add(company);
        db.Users.Add(user);

        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "valid-token-123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ValidateRefreshTokenAsync("valid-token-123");

        result.Should().NotBeNull();
        result!.Token.Should().Be("valid-token-123");
        result.User.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsNullForRevokedToken()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var user = TestHelpers.CreateTestUser(Guid.NewGuid());
        db.Users.Add(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "revoked-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = true
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ValidateRefreshTokenAsync("revoked-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsNullForExpiredToken()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var user = TestHelpers.CreateTestUser(Guid.NewGuid());
        db.Users.Add(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ValidateRefreshTokenAsync("expired-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_ReturnsNullForNonExistentToken()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);

        var result = await service.ValidateRefreshTokenAsync("does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_MarksTokenAsRevoked()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var user = TestHelpers.CreateTestUser(Guid.NewGuid());
        db.Users.Add(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "to-revoke",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RevokeRefreshTokenAsync("to-revoke");

        var token = await db.RefreshTokens.FirstAsync(rt => rt.Token == "to-revoke");
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_DoesNothingForNonExistentToken()
    {
        var db = TestHelpers.CreateInMemoryDbContext();
        var service = CreateService(db);

        // Should not throw
        await service.RevokeRefreshTokenAsync("nonexistent-token");
    }
}
