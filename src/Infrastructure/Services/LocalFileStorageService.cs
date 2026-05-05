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
        var taskDir = Path.Combine(_basePath, taskId.ToString());
        Directory.CreateDirectory(taskDir);

        var sanitizedName = Path.GetFileName(fileName);
        var storedFileName = $"{Guid.NewGuid()}_{sanitizedName}";
        var filePath = Path.Combine(taskDir, storedFileName);

        ValidatePathWithinBase(filePath);

        using var output = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(output, cancellationToken);

        return filePath;
    }

    public async Task<(string storedPath, string fileName)> SaveImageAsync(string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var imagesDir = Path.Combine(_basePath, "images");
        Directory.CreateDirectory(imagesDir);

        var sanitizedName = Path.GetFileName(fileName);
        var storedFileName = $"{Guid.NewGuid()}_{sanitizedName}";
        var filePath = Path.Combine(imagesDir, storedFileName);

        ValidatePathWithinBase(filePath);

        using var output = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(output, cancellationToken);

        return (filePath, storedFileName);
    }

    public Task<Stream> GetFileAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        ValidatePathWithinBase(storedPath);

        if (!File.Exists(storedPath))
            throw new FileNotFoundException("File not found.", storedPath);

        Stream stream = new FileStream(storedPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        ValidatePathWithinBase(storedPath);

        if (File.Exists(storedPath))
            File.Delete(storedPath);

        return Task.CompletedTask;
    }

    private void ValidatePathWithinBase(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access to the specified path is denied.");
    }
}
