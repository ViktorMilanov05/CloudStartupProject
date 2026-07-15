using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class NotificationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _retentionPeriod;

    public NotificationCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromHours(configuration.GetValue<double?>("Cleanup:IntervalHours") ?? 24);
        _retentionPeriod = TimeSpan.FromDays(configuration.GetValue<double?>("Cleanup:NotificationRetentionDays") ?? 30);
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

            await Task.Delay(_interval, stoppingToken);
        }
    }

    internal async Task CleanupOldNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(_retentionPeriod);
        var deleted = await dbContext.Notifications
            .Where(n => n.CreatedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} notifications older than {Days} days", deleted, _retentionPeriod.TotalDays);
    }
}
