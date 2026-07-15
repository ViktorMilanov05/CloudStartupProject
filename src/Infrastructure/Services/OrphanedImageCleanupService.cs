using System.Text.RegularExpressions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Periodically removes inline image files that are no longer referenced by any
/// rich-text content (task descriptions, step instructions, comments). Files are
/// only considered for deletion after a grace period so freshly uploaded images
/// that have not yet been saved into content are never removed.
/// </summary>
public partial class OrphanedImageCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrphanedImageCleanupService> _logger;
    private readonly string _imagesPath;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _retentionPeriod;

    public OrphanedImageCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrphanedImageCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var basePath = Path.GetFullPath(
            configuration["FileStorage:BasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "Uploads"));
        _imagesPath = Path.Combine(basePath, "images");

        _interval = TimeSpan.FromHours(configuration.GetValue<double?>("Cleanup:IntervalHours") ?? 24);
        _retentionPeriod = TimeSpan.FromDays(configuration.GetValue<double?>("Cleanup:OrphanedImageRetentionDays") ?? 7);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOrphanedImagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned image cleanup");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    internal async Task CleanupOrphanedImagesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_imagesPath))
            return;

        var cutoff = DateTime.UtcNow.Subtract(_retentionPeriod);
        var candidates = new DirectoryInfo(_imagesPath)
            .GetFiles()
            .Where(f => f.LastWriteTimeUtc <= cutoff)
            .ToList();

        if (candidates.Count == 0)
            return;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var referenced = await GetReferencedImageNamesAsync(dbContext, cancellationToken);

        var deleted = 0;
        foreach (var file in candidates)
        {
            if (referenced.Contains(file.Name))
                continue;

            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphaned image '{Name}'", file.Name);
            }
        }

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} orphaned images", deleted);
    }

    private static async Task<HashSet<string>> GetReferencedImageNamesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var contents = new List<string>();
        contents.AddRange(await dbContext.Tasks
            .Where(t => t.Description != null)
            .Select(t => t.Description!)
            .ToListAsync(cancellationToken));
        contents.AddRange(await dbContext.TaskSteps
            .Where(s => s.Instructions != null)
            .Select(s => s.Instructions!)
            .ToListAsync(cancellationToken));
        contents.AddRange(await dbContext.TaskComments
            .Select(c => c.Content)
            .ToListAsync(cancellationToken));
        contents.AddRange(await dbContext.TemplateSteps
            .Where(s => s.Instructions != null)
            .Select(s => s.Instructions!)
            .ToListAsync(cancellationToken));

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var regex = ImageReferenceRegex();
        foreach (var content in contents)
        {
            if (string.IsNullOrEmpty(content))
                continue;

            foreach (Match match in regex.Matches(content))
                referenced.Add(Uri.UnescapeDataString(match.Groups[1].Value));
        }

        return referenced;
    }

    [GeneratedRegex(@"/api/files/images/([^""'\s\)\?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ImageReferenceRegex();
}
