using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using static API.IntegrationTests.IntegrationTestHelpers;

namespace API.IntegrationTests;

public class FilesControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FilesControllerTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<(string ManagerToken, Guid ManagerId, Guid TaskId)> SetupManagerWithTaskAsync()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var createResp = await _client.PostAsJsonAsync("/api/admin/companies", new
        {
            companyName = "FileTestCorp",
            managerEmail = $"mgr-{Guid.NewGuid():N}@test.com",
            managerPassword = "ManagerPass1!",
            managerFirstName = "Mgr",
            managerLastName = "Files"
        });
        createResp.EnsureSuccessStatusCode();
        var company = await createResp.Content.ReadFromJsonAsync<CompanyResponse>();
        var users = await _client.GetFromJsonAsync<List<UserResponse>>($"/api/admin/companies/{company!.Id}/users");

        _client.DefaultRequestHeaders.Authorization = null;
        var mgrAuth = await LoginAsync(_client, users!.First().Email, "ManagerPass1!");
        Authenticate(_client, mgrAuth.AccessToken);

        // Create a task
        var taskResp = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "File Test Task",
            priority = "Medium",
            assigneeIds = new[] { users.First().Id }
        });
        var task = await taskResp.Content.ReadFromJsonAsync<TaskResponse>();

        return (mgrAuth.AccessToken, users.First().Id, task!.Id);
    }

    [Fact]
    public async Task UploadImage_Unauthorized_Returns401()
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[10]), "file", "test.png");
        var resp = await _client.PostAsync("/api/files/upload-image", content);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadImage_NoFile_ReturnsBadRequest()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var content = new MultipartFormDataContent();
        var resp = await _client.PostAsync("/api/files/upload-image", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadImage_InvalidExtension_ReturnsBadRequest()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[100]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");

        var resp = await _client.PostAsync("/api/files/upload-image", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadImage_ValidPng_ReturnsUrl()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[100]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");

        var resp = await _client.PostAsync("/api/files/upload-image", content);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<ImageUploadResponse>();
        result!.Url.Should().StartWith("/api/files/images/");
    }

    [Fact]
    public async Task GetImage_PathTraversal_ReturnsBadRequest()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var resp = await _client.GetAsync("/api/files/images/..%2F..%2Fetc%2Fpasswd");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetImage_Nonexistent_Returns404()
    {
        var admin = await SetupAdminAsync(_client);
        Authenticate(_client, admin.AccessToken);

        var resp = await _client.GetAsync("/api/files/images/nonexistent.png");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadAttachment_InvalidExtension_ReturnsBadRequest()
    {
        var (_, _, taskId) = await SetupManagerWithTaskAsync();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[100]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/exe");
        content.Add(fileContent, "file", "malware.exe");

        var resp = await _client.PostAsync($"/api/files/upload?taskId={taskId}", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_NoFile_ReturnsBadRequest()
    {
        var (_, _, taskId) = await SetupManagerWithTaskAsync();

        var content = new MultipartFormDataContent();
        var resp = await _client.PostAsync($"/api/files/upload?taskId={taskId}", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_ValidPdf_ReturnsOk()
    {
        var (_, _, taskId) = await SetupManagerWithTaskAsync();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[100]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "document.pdf");

        var resp = await _client.PostAsync($"/api/files/upload?taskId={taskId}", content);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DownloadAttachment_Unauthorized_Returns401()
    {
        var resp = await _client.GetAsync($"/api/files/{Guid.NewGuid()}?taskId={Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAttachment_Unauthorized_Returns401()
    {
        var resp = await _client.DeleteAsync($"/api/files/{Guid.NewGuid()}?taskId={Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public record TaskResponse(Guid Id, string Title);
    public record ImageUploadResponse(string Url);
}
