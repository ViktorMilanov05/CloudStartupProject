using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using static API.IntegrationTests.IntegrationTestHelpers;

namespace API.IntegrationTests;

public class AdminControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminControllerTests()
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
    public async Task GetCompanies_AsAdmin_ReturnsOk()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var response = await _client.GetAsync("/api/admin/companies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateCompany_AsAdmin_ReturnsCreated()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "New Company",
            managerEmail = $"new-mgr-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "New",
            managerLastName = "Manager"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetCompanies_AsManager_Returns403()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var createResp = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "ForbidCorp",
            managerEmail = $"mgr-forbid-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "Forbid",
            managerLastName = "Mgr"
        });
        var company = await createResp.Content.ReadFromJsonAsync<CompanyResponse>();
        var users = await _client.GetFromJsonAsync<List<UserResponse>>($"/api/admin/companies/{company!.Id}/users");

        _client.DefaultRequestHeaders.Authorization = null;
        var mgrAuth = await LoginAsync(_client, users!.First().Email, "ManagerPass1!");
        Authenticate(_client, mgrAuth.AccessToken);

        var response = await _client.GetAsync("/api/admin/companies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCompanies_Unauthorized_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/companies");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCompanyUser_AsAdmin_Succeeds()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var compResp = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "UserCreateCorp",
            managerEmail = $"mgr-cu-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "CU",
            managerLastName = "Mgr"
        });
        var company = await compResp.Content.ReadFromJsonAsync<CompanyResponse>();

        var userResp = await _client.PostAsJsonAsync($"/api/admin/companies/{company!.Id}/users", new
        {
            email = $"created-{Guid.NewGuid():N}@test.com",
            password = "UserPass123!",
            firstName = "Created",
            lastName = "User",
            role = "User"
        });

        userResp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateUser_AsAdmin_Succeeds()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var compResp = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "UpdateUserCorp",
            managerEmail = $"mgr-uu-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "UU",
            managerLastName = "Mgr"
        });
        var company = await compResp.Content.ReadFromJsonAsync<CompanyResponse>();
        var users = await _client.GetFromJsonAsync<List<UserResponse>>($"/api/admin/companies/{company!.Id}/users");

        var updateResp = await _client.PutAsJsonAsync($"/api/admin/users/{users!.First().Id}", new
        {
            firstName = "Updated"
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
