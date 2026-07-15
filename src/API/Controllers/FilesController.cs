using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ITaskService _taskService;

    private static readonly HashSet<string> AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const long MaxImageSize = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedAttachmentExtensions =
    [
        ".pdf", ".docx", ".xlsx", ".pptx", ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".txt", ".csv", ".zip", ".mp4"
    ];
    private const long MaxAttachmentSize = 25 * 1024 * 1024; // 25 MB

    public FilesController(IFileStorageService fileStorageService, ITaskService taskService)
    {
        _fileStorageService = fileStorageService;
        _taskService = taskService;
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxImageSize)
            return BadRequest(new { error = "File size exceeds 5 MB limit." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
            return BadRequest(new { error = "Only image files (jpg, png, gif, webp) are allowed." });

        using var stream = file.OpenReadStream();
        var (storedPath, storedFileName) = await _fileStorageService.SaveImageAsync(file.FileName, stream, cancellationToken);

        var imageUrl = $"{Request.Scheme}://{Request.Host}/api/files/images/{storedFileName}";

        return Ok(new { url = imageUrl });
    }

    [HttpGet("images/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(string fileName, CancellationToken cancellationToken)
    {
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(sanitizedFileName) || sanitizedFileName != fileName)
            return BadRequest();

        try
        {
            var basePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Uploads",
                "images",
                sanitizedFileName);

            var stream = await _fileStorageService.GetFileAsync(basePath, cancellationToken);
            var contentType = GetContentType(sanitizedFileName);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Manager,User")]
    public async Task<IActionResult> UploadAttachment([FromQuery] Guid taskId, [FromQuery] Guid? commentId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxAttachmentSize)
            return BadRequest(new { error = "File size exceeds 25 MB limit." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(extension))
            return BadRequest(new { error = $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedAttachmentExtensions)}" });

        var (userId, companyId, role) = GetCallerContext();

        using var stream = file.OpenReadStream();
        var attachment = await _taskService.AddAttachmentAsync(
            taskId, commentId, userId, companyId, role,
            file.FileName, file.ContentType, file.Length, stream,
            cancellationToken);

        return Ok(attachment);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Manager,User")]
    public async Task<IActionResult> DownloadAttachment(Guid id, [FromQuery] Guid taskId, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();

        var (stream, fileName, contentType) = await _taskService.DownloadAttachmentAsync(
            taskId, id, userId, companyId, role, cancellationToken);

        return File(stream, contentType, fileName);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Manager,User")]
    public async Task<IActionResult> DeleteAttachment(Guid id, [FromQuery] Guid taskId, CancellationToken cancellationToken)
    {
        var (userId, companyId, role) = GetCallerContext();

        await _taskService.DeleteAttachmentAsync(taskId, id, userId, companyId, role, cancellationToken);

        return NoContent();
    }

    private (Guid UserId, Guid CompanyId, string Role) GetCallerContext()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        var companyIdStr = User.FindFirstValue("companyId")
            ?? throw new UnauthorizedAccessException("Company ID claim not found.");
        var role = User.FindFirstValue(ClaimTypes.Role)
            ?? throw new UnauthorizedAccessException("Role claim not found.");

        var companyId = string.IsNullOrEmpty(companyIdStr) ? Guid.Empty : Guid.Parse(companyIdStr);
        return (Guid.Parse(userIdStr), companyId, role);
    }
}
