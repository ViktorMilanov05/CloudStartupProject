namespace Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Guid taskId, string fileName, Stream fileStream, CancellationToken cancellationToken = default);
    Task<(string storedPath, string fileName)> SaveImageAsync(string fileName, Stream fileStream, CancellationToken cancellationToken = default);
    Task<Stream> GetFileAsync(string storedPath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string storedPath, CancellationToken cancellationToken = default);
}
