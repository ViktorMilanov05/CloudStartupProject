using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace API.IntegrationTests;

public class SetupControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SetupControllerTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetStatus_ReturnsSetupRequired_WhenNoAdminExists()
    {
        var response = await _client.GetAsync("/api/setup/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        content!.SetupRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Initialize_CreatesAdminAndReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/setup/initialize", new
        {
            email = "admin@test.com",
            password = "StrongPass123!",
            firstName = "System",
            lastName = "Admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        content!.AccessToken.Should().NotBeNullOrEmpty();
        content.User.Email.Should().Be("admin@test.com");
        content.User.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Initialize_ReturnsNotFound_WhenAdminAlreadyExists()
    {
        // First, create the admin
        await _client.PostAsJsonAsync("/api/setup/initialize", new
        {
            email = "admin2@test.com",
            password = "StrongPass123!",
            firstName = "System",
            lastName = "Admin"
        });

        // Second attempt should fail
        var response = await _client.PostAsJsonAsync("/api/setup/initialize", new
        {
            email = "admin3@test.com",
            password = "AnotherPass123!",
            firstName = "Another",
            lastName = "Admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record SetupStatusResponse(bool SetupRequired);
    private record AuthUserDto(string Email, string Role);
    private record AuthResponseDto(string AccessToken, AuthUserDto User);
}
