namespace Application.DTOs.Tasks;

public class TaskDetailDto : TaskItemDto
{
    public List<TaskStepDto> Steps { get; set; } = [];
    public List<TaskCommentDto> Comments { get; set; } = [];
    public List<TaskAttachmentDto> Attachments { get; set; } = [];
}
