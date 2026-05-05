using Application.DTOs;
using Application.DTOs.Tasks;

namespace Application.Interfaces;

public interface ITaskService
{
    Task<PagedResult<TaskItemDto>> GetAllAsync(Guid userId, Guid companyId, string userRole, TaskFilterRequest filter, CancellationToken cancellationToken = default);
    Task<TaskDetailDto?> GetByIdAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> CreateAsync(Guid createdById, Guid companyId, string userRole, CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> CreateFromTemplateAsync(Guid templateId, Guid createdById, Guid companyId, string userRole, CreateTaskFromTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> UpdateAsync(Guid taskId, Guid userId, Guid companyId, string userRole, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid taskId, Guid userId, Guid companyId, CancellationToken cancellationToken = default);

    Task<TaskStepDto> AddStepAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CreateTaskStepRequest request, CancellationToken cancellationToken = default);
    Task<TaskStepDto> UpdateStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, UpdateTaskStepRequest request, CancellationToken cancellationToken = default);
    Task<string?> CompleteStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
    Task<string?> UncompleteStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
    Task DeleteStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
    Task ReorderStepsAsync(Guid taskId, Guid userId, Guid companyId, string userRole, ReorderTaskStepsRequest request, CancellationToken cancellationToken = default);

    // Comments
    Task<List<TaskCommentDto>> GetCommentsAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
    Task<TaskCommentDto> AddCommentAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CreateTaskCommentRequest request, CancellationToken cancellationToken = default);
    Task<TaskCommentDto> UpdateCommentAsync(Guid taskId, Guid commentId, Guid userId, Guid companyId, string userRole, UpdateTaskCommentRequest request, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(Guid taskId, Guid commentId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);

    // Attachments
    Task<TaskAttachmentDto> AddAttachmentAsync(Guid taskId, Guid? commentId, Guid userId, Guid companyId, string userRole, string fileName, string contentType, long fileSize, Stream fileStream, CancellationToken cancellationToken = default);
    Task DeleteAttachmentAsync(Guid taskId, Guid attachmentId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string FileName, string ContentType)> DownloadAttachmentAsync(Guid taskId, Guid attachmentId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default);
}
