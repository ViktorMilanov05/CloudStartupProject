using System.IdentityModel.Tokens.Jwt;
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

        // Read into memory so the file signature can be validated before persisting.
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        if (!HasValidImageSignature(memoryStream, extension))
            return BadRequest(new { error = "File content does not match a valid image." });

        memoryStream.Position = 0;
        var (storedPath, storedFileName) = await _fileStorageService.SaveImageAsync(file.FileName, memoryStream, cancellationToken);

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
            var relativePath = Path.Combine("images", sanitizedFileName);
            var stream = await _fileStorageService.GetFileAsync(relativePath, cancellationToken);
            var contentType = GetContentType(sanitizedFileName);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
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

    /// <summary>
    /// Verifies the leading bytes of the uploaded stream match the expected image
    /// format for the given extension, preventing disguised non-image uploads.
    /// </summary>
    private static bool HasValidImageSignature(Stream stream, string extension)
    {
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read < 12)
            return false;

        return extension switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                      && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".gif" => header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,
            ".webp" => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                       && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
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
        var userIdStr = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        var companyIdStr = User.FindFirstValue("companyId");
        var role = User.FindFirstValue("role")
            ?? throw new UnauthorizedAccessException("Role claim not found.");

        var companyId = string.IsNullOrEmpty(companyIdStr) ? Guid.Empty : Guid.Parse(companyIdStr);
        return (Guid.Parse(userIdStr), companyId, role);
    }
}
