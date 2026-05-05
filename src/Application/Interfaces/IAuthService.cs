using Application.DTOs;
using Application.DTOs.Auth;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);
    Task<AuthResponse> SetupAdminAsync(SetupRequest request, CancellationToken cancellationToken = default);
}
