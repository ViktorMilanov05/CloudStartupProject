namespace Domain.Entities;

public class TaskAttachment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid? CommentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public TaskComment? Comment { get; set; }
    public User UploadedBy { get; set; } = null!;
}
