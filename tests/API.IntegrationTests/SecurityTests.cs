using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using static API.IntegrationTests.IntegrationTestHelpers;

namespace API.IntegrationTests;

public class SecurityTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityTests()
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
    public async Task Response_HasSecurityHeaders()
    {
        var response = await _client.GetAsync("/api/health");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");

        response.Headers.Should().ContainKey("Referrer-Policy");
    }

    [Fact]
    public async Task Response_HasCspHeader()
    {
        var response = await _client.GetAsync("/api/health");

        response.Headers.Should().ContainKey("Content-Security-Policy");
        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("script-src 'self'");
        csp.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task ImageEndpoint_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/files/images/nonexistent.jpg");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadEndpoint_RequiresAuth()
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[10]), "file", "test.pdf");
        var response = await _client.PostAsync("/api/files/upload?taskId=" + Guid.NewGuid(), content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TasksEndpoint_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/tasks");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TemplatesEndpoint_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/templates");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_RequiresAdminRole()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var compResp = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "SecurityTestCorp",
            managerEmail = $"mgr-sec-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "Sec",
            managerLastName = "Mgr"
        });
        var company = await compResp.Content.ReadFromJsonAsync<CompanyResponse>();
        var users = await _client.GetFromJsonAsync<List<UserResponse>>($"/api/admin/companies/{company!.Id}/users");

        _client.DefaultRequestHeaders.Authorization = null;
        var mgrAuth = await LoginAsync(_client, users!.First().Email, "ManagerPass1!");
        Authenticate(_client, mgrAuth.AccessToken);

        var companiesResp = await _client.GetAsync("/api/admin/companies");
        companiesResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NotificationsEndpoint_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_OnlyWorksOnce()
    {
        var firstResp = await _client.PostAsJsonAsync("/api/setup/initialize", new
        {
            email = $"setup-once-{Guid.NewGuid():N}@test.com",
            password = "StrongPass123!",
            firstName = "First",
            lastName = "Admin"
        });
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResp = await _client.PostAsJsonAsync("/api/setup/initialize", new
        {
            email = $"second-{Guid.NewGuid():N}@test.com",
            password = "StrongPass123!",
            firstName = "Second",
            lastName = "Admin"
        });
        secondResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
