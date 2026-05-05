namespace Application.DTOs.Tasks;

public class TaskAttachmentDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid? CommentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
