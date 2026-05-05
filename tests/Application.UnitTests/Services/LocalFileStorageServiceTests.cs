using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Application.UnitTests.Services;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _testBasePath;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), $"FileStorageTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testBasePath);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = _testBasePath
            })
            .Build();

        _service = new LocalFileStorageService(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBasePath))
            Directory.Delete(_testBasePath, true);
    }

    [Fact]
    public async Task SaveFileAsync_CreatesFileOnDisk()
    {
        var taskId = Guid.NewGuid();
        var content = "test file content"u8.ToArray();
        using var stream = new MemoryStream(content);

        var storedPath = await _service.SaveFileAsync(taskId, "test.pdf", stream);

        File.Exists(storedPath).Should().BeTrue();
        var savedContent = await File.ReadAllBytesAsync(storedPath);
        savedContent.Should().Equal(content);
    }

    [Fact]
    public async Task SaveFileAsync_SanitizesFileName()
    {
        var taskId = Guid.NewGuid();
        using var stream = new MemoryStream("data"u8.ToArray());

        var storedPath = await _service.SaveFileAsync(taskId, "../../../etc/passwd", stream);

        storedPath.Should().StartWith(_testBasePath);
        Path.GetFileName(storedPath).Should().NotContain("..");
    }

    [Fact]
    public async Task SaveImageAsync_StoresInImagesDirectory()
    {
        using var stream = new MemoryStream("image data"u8.ToArray());

        var (storedPath, fileName) = await _service.SaveImageAsync("photo.jpg", stream);

        storedPath.Should().Contain(Path.Combine(_testBasePath, "images"));
        File.Exists(storedPath).Should().BeTrue();
        fileName.Should().EndWith("_photo.jpg");
    }

    [Fact]
    public async Task GetFileAsync_ReturnsStream()
    {
        var taskId = Guid.NewGuid();
        var content = "read test"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        var storedPath = await _service.SaveFileAsync(taskId, "readable.txt", writeStream);

        using var readStream = await _service.GetFileAsync(storedPath);

        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task GetFileAsync_ThrowsWhenNotFound()
    {
        var act = () => _service.GetFileAsync(Path.Combine(_testBasePath, "nonexistent.txt"));
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFile()
    {
        var taskId = Guid.NewGuid();
        using var stream = new MemoryStream("delete me"u8.ToArray());
        var storedPath = await _service.SaveFileAsync(taskId, "deletable.txt", stream);

        await _service.DeleteFileAsync(storedPath);

        File.Exists(storedPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFileAsync_NoErrorWhenFileDoesNotExist()
    {
        var act = () => _service.DeleteFileAsync(Path.Combine(_testBasePath, "already-gone.txt"));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetFileAsync_PathTraversal_Throws()
    {
        var maliciousPath = Path.Combine(_testBasePath, "..", "..", "etc", "passwd");
        var act = () => _service.GetFileAsync(maliciousPath);
        act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void DeleteFileAsync_PathTraversal_Throws()
    {
        var maliciousPath = Path.Combine(_testBasePath, "..", "..", "etc", "passwd");
        var act = () => _service.DeleteFileAsync(maliciousPath);
        act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
