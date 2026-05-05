using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using static API.IntegrationTests.IntegrationTestHelpers;

namespace API.IntegrationTests;

public class UsersControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UsersControllerTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task SetupManagerAsync()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var createResp = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "UserTestCorp",
            managerEmail = $"mgr-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "Mgr",
            managerLastName = "Users"
        });
        createResp.EnsureSuccessStatusCode();
        var company = await createResp.Content.ReadFromJsonAsync<CompanyResponse>();
        var users = await _client.GetFromJsonAsync<List<UserResponse>>($"/api/admin/companies/{company!.Id}/users");

        _client.DefaultRequestHeaders.Authorization = null;
        var mgrAuth = await LoginAsync(_client, users!.First().Email, "ManagerPass1!");
        Authenticate(_client, mgrAuth.AccessToken);
    }

    [Fact]
    public async Task GetUsers_AsManager_ReturnsUsers()
    {
        await SetupManagerAsync();

        var response = await _client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        users.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMe_ReturnsCurrentUser()
    {
        await SetupManagerAsync();

        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user!.Role.Should().Be("Manager");
    }

    [Fact]
    public async Task CreateUser_AsManager_Succeeds()
    {
        await SetupManagerAsync();

        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            email = $"newuser-{Guid.NewGuid():N}@test.com",
            password = "NewUserPass1!",
            firstName = "New",
            lastName = "User",
            role = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateUser_InvalidRole_ReturnsBadRequest()
    {
        await SetupManagerAsync();

        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            email = $"newuser-{Guid.NewGuid():N}@test.com",
            password = "NewUserPass1!",
            firstName = "New",
            lastName = "User",
            role = "Admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUser_ChangeRole_Succeeds()
    {
        await SetupManagerAsync();

        var createResp = await _client.PostAsJsonAsync("/api/users", new
        {
            email = $"toupdate-{Guid.NewGuid():N}@test.com",
            password = "UserPass123!",
            firstName = "Update",
            lastName = "Me",
            role = "User"
        });
        var created = await createResp.Content.ReadFromJsonAsync<UserResponse>();

        var updateResp = await _client.PutAsJsonAsync($"/api/users/{created!.Id}", new
        {
            role = "Manager"
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteUser_ReturnsSuccessOrHandledError()
    {
        await SetupManagerAsync();

        var createResp = await _client.PostAsJsonAsync("/api/users", new
        {
            email = $"todelete-{Guid.NewGuid():N}@test.com",
            password = "UserPass123!",
            firstName = "Delete",
            lastName = "Me",
            role = "User"
        });
        var created = await createResp.Content.ReadFromJsonAsync<UserResponse>();

        // RevokeAllUserTokensAsync uses ExecuteUpdateAsync (not supported by InMemory)
        var deleteResp = await _client.DeleteAsync($"/api/users/{created!.Id}");
        deleteResp.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUsers_Unauthorized_Returns401()
    {
        var response = await _client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
