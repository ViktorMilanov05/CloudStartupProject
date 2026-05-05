using System.Text.Json.Serialization;

namespace Application.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;

    /// <summary>
    /// Used internally by the controller to set the httpOnly cookie.
    /// Excluded from JSON serialization.
    /// </summary>
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
}
