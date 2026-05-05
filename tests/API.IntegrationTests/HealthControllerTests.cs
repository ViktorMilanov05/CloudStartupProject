using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace API.IntegrationTests;

public class HealthControllerTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthControllerTests()
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
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        content!.Status.Should().Be("healthy");
    }

    private record HealthResponse(string Status, DateTime Timestamp);
}
