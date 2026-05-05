using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class NotificationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public NotificationCleanupService(IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupService> logger)
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
                await CleanupOldNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification cleanup");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    internal async Task CleanupOldNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(RetentionPeriod);
        var deleted = await dbContext.Notifications
            .Where(n => n.CreatedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} notifications older than 30 days", deleted);
    }
}
