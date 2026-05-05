using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace API.IntegrationTests;

public static class IntegrationTestHelpers
{
    public record AuthResponse(string AccessToken, AuthUser User);
    public record AuthUser(string Email, string Role, string Id);

    public static async Task<AuthResponse> SetupAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/setup/initialize", new
        {
            email = "admin@integration.com",
            password = "StrongPass123!",
            firstName = "Test",
            lastName = "Admin"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static void Authenticate(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public record CompanyResponse(Guid Id, string Name);
    public record UserResponse(Guid Id, string Email, string FirstName, string LastName, string Role);
}
