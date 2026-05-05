using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class TokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during refresh token cleanup");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow;
        var deleted = await dbContext.RefreshTokens
            .Where(rt => rt.IsRevoked || rt.ExpiresAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} expired/revoked refresh tokens", deleted);
    }
}
