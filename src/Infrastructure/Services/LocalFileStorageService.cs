using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = Path.GetFullPath(
            configuration["FileStorage:BasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "Uploads"));
    }

    public async Task<string> SaveFileAsync(Guid taskId, string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var relativeDir = taskId.ToString();
        Directory.CreateDirectory(Path.Combine(_basePath, relativeDir));

        var sanitizedName = Path.GetFileName(fileName);
        var storedFileName = $"{Guid.NewGuid()}_{sanitizedName}";
        var relativePath = Path.Combine(relativeDir, storedFileName);
        var fullPath = ResolveAndValidate(relativePath);

        using var output = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(output, cancellationToken);

        return relativePath;
    }

    public async Task<(string storedPath, string fileName)> SaveImageAsync(string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        const string relativeDir = "images";
        Directory.CreateDirectory(Path.Combine(_basePath, relativeDir));

        var sanitizedName = Path.GetFileName(fileName);
        var storedFileName = $"{Guid.NewGuid()}_{sanitizedName}";
        var relativePath = Path.Combine(relativeDir, storedFileName);
        var fullPath = ResolveAndValidate(relativePath);

        using var output = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(output, cancellationToken);

        return (relativePath, storedFileName);
    }

    public Task<Stream> GetFileAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveAndValidate(storedPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveAndValidate(storedPath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a stored path to a full path, then verifies it stays within the
    /// configured storage root. Accepts both new relative paths (relative to the
    /// base path) and legacy absolute paths written by older versions.
    /// </summary>
    private string ResolveAndValidate(string storedPath)
    {
        var fullPath = Path.GetFullPath(
            Path.IsPathRooted(storedPath)
                ? storedPath
                : Path.Combine(_basePath, storedPath));

        // Ensure the resolved path stays within the storage root. Comparing against
        // the root with a trailing separator prevents a sibling directory whose name
        // merely shares the root as a prefix (e.g. "Uploads_evil") from passing.
        var rootWithSeparator = _basePath.EndsWith(Path.DirectorySeparatorChar)
            ? _basePath
            : _basePath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access to the specified path is denied.");

        return fullPath;
    }
}
