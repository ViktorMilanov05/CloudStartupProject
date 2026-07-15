using Application.DTOs;
using Application.DTOs.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<TaskService> _logger;
    private readonly IFileStorageService _fileStorageService;
    private readonly INotificationService _notificationService;

    private static readonly Dictionary<TaskItemStatus, HashSet<TaskItemStatus>> ValidTransitions = new()
    {
        [TaskItemStatus.ToDo] = [TaskItemStatus.InProgress, TaskItemStatus.Blocked],
        [TaskItemStatus.InProgress] = [TaskItemStatus.ToDo, TaskItemStatus.Done, TaskItemStatus.Blocked],
        [TaskItemStatus.Blocked] = [TaskItemStatus.ToDo, TaskItemStatus.InProgress],
        [TaskItemStatus.Done] = [TaskItemStatus.InProgress],
    };

    public TaskService(AppDbContext dbContext, UserManager<User> userManager, ILogger<TaskService> logger, IFileStorageService fileStorageService, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
        _fileStorageService = fileStorageService;
        _notificationService = notificationService;
    }

    public async Task<PagedResult<TaskItemDto>> GetAllAsync(Guid userId, Guid companyId, string userRole, TaskFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .Include(t => t.SourceTemplate)
            .Include(t => t.Steps)
            .AsQueryable();

        // Visibility: Admin sees all tasks, Manager sees all company tasks, User sees only assigned
        if (userRole == "Admin")
        {
            // No additional filter — admins oversee tasks across all companies.
        }
        else if (userRole == "Manager")
            query = query.Where(t => t.Assignees.Any(a => a.CompanyId == companyId) || t.CreatedBy.CompanyId == companyId);
        else
            query = query.Where(t => t.Assignees.Any(a => a.Id == userId));

        // Filters
        if (!string.IsNullOrEmpty(filter.Status) && Enum.TryParse<TaskItemStatus>(filter.Status, out var status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrEmpty(filter.Priority) && Enum.TryParse<TaskPriority>(filter.Priority, out var priority))
            query = query.Where(t => t.Priority == priority);

        if (filter.AssigneeId.HasValue)
            query = query.Where(t => t.Assignees.Any(a => a.Id == filter.AssigneeId.Value));

        if (filter.DueDateFrom.HasValue)
            query = query.Where(t => t.DueDate >= filter.DueDateFrom.Value);

        if (filter.DueDateTo.HasValue)
            query = query.Where(t => t.DueDate <= filter.DueDateTo.Value);

        if (!string.IsNullOrEmpty(filter.Search))
            query = query.Where(t => t.Title.Contains(filter.Search) || (t.Description != null && t.Description.Contains(filter.Search)));

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "title" => filter.SortDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
            "status" => filter.SortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "priority" => filter.SortDescending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
            "duedate" => filter.SortDescending ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
            "createdat" => filter.SortDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.UpdatedAt),
        };

        // Pagination
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var tasks = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskItemDto>
        {
            Items = tasks.Select(MapToItemDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<TaskDetailDto?> GetByIdAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .Include(t => t.SourceTemplate)
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.CompletedBy)
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .Include(t => t.Comments)
                .ThenInclude(c => c.Attachments)
                    .ThenInclude(a => a.UploadedBy)
            .Include(t => t.Attachments.Where(a => a.CommentId == null))
                .ThenInclude(a => a.UploadedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
            return null;

        if (!HasAccess(task, userId, companyId, userRole))
            return null;

        return MapToDetailDto(task);
    }

    public async Task<TaskDetailDto> CreateAsync(Guid createdById, Guid companyId, string userRole, CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        // Users can only assign to themselves
        if (userRole == "User" && (request.AssigneeIds.Count != 1 || request.AssigneeIds[0] != createdById))
            throw new InvalidOperationException("Users can only assign tasks to themselves.");

        // Managers: verify all assignees are in the same company
        if (userRole == "Manager")
        {
            foreach (var assigneeId in request.AssigneeIds)
                await ValidateAssigneeInCompany(assigneeId, companyId, cancellationToken);
        }

        var assignees = await _userManager.Users
            .Where(u => request.AssigneeIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Status = TaskItemStatus.ToDo,
            Priority = Enum.Parse<TaskPriority>(request.Priority),
            DueDate = request.DueDate,
            CreatedById = createdById,
            Assignees = assignees,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task '{TaskTitle}' created by {UserId}", task.Title, createdById);

        var actorName = await GetUserFullName(createdById, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.TaskAssigned, createdById, actorName,
            task.Id, task.Title,
            $"{actorName} assigned you to \"{task.Title}\"",
            request.AssigneeIds, cancellationToken);

        return await ReloadTaskDetailAsync(task.Id, cancellationToken);
    }

    public async Task<TaskDetailDto> CreateFromTemplateAsync(Guid templateId, Guid createdById, Guid companyId, string userRole, CreateTaskFromTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.Templates
            .AsNoTracking()
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == templateId && t.IsActive
                && (userRole == "Admin" || t.CompanyId == companyId), cancellationToken);

        if (template is null)
            throw new KeyNotFoundException("Template not found or is inactive.");

        // Users can only assign to themselves
        if (userRole == "User" && (request.AssigneeIds.Count != 1 || request.AssigneeIds[0] != createdById))
            throw new InvalidOperationException("Users can only assign tasks to themselves.");

        if (userRole == "Manager")
        {
            foreach (var assigneeId in request.AssigneeIds)
                await ValidateAssigneeInCompany(assigneeId, companyId, cancellationToken);
        }

        var assignees = await _userManager.Users
            .Where(u => request.AssigneeIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? template.Name,
            Description = request.Description ?? template.Description,
            Status = TaskItemStatus.ToDo,
            Priority = Enum.Parse<TaskPriority>(request.Priority),
            DueDate = request.DueDate,
            CreatedById = createdById,
            Assignees = assignees,
            SourceTemplateId = templateId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Snapshot template steps into task steps
        foreach (var templateStep in template.Steps)
        {
            task.Steps.Add(new TaskStep
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                Title = templateStep.Title,
                Instructions = templateStep.Instructions,
                SortOrder = templateStep.SortOrder,
                IsCompleted = false,
            });
        }

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task '{TaskTitle}' created from template '{TemplateId}' by {UserId}", task.Title, templateId, createdById);

        var actorNameT = await GetUserFullName(createdById, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.TaskAssigned, createdById, actorNameT,
            task.Id, task.Title,
            $"{actorNameT} assigned you to \"{task.Title}\"",
            request.AssigneeIds, cancellationToken);

        return await ReloadTaskDetailAsync(task.Id, cancellationToken);
    }

    public async Task<TaskDetailDto> UpdateAsync(Guid taskId, Guid userId, Guid companyId, string userRole, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .Include(t => t.SourceTemplate)
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.CompletedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
            throw new KeyNotFoundException("Task not found.");

        if (!HasAccess(task, userId, companyId, userRole))
            throw new UnauthorizedAccessException("You do not have access to this task.");

        // Users cannot reassign tasks
        if (userRole == "User" && request.AssigneeIds is not null)
            throw new InvalidOperationException("Users cannot reassign tasks.");

        // Managers: verify new assignees are in company
        if (request.AssigneeIds is not null)
        {
            foreach (var assigneeId in request.AssigneeIds)
                await ValidateAssigneeInCompany(assigneeId, companyId, cancellationToken);
        }

        // Track changes for notifications
        var oldStatus = task.Status;
        var oldAssigneeIds = task.Assignees.Select(a => a.Id).ToHashSet();
        var hasNonStatusEdit = request.Title is not null || request.Description is not null
            || request.Priority is not null || request.DueDate.HasValue;

        // Status transition validation
        if (request.Status is not null)
        {
            var newStatus = Enum.Parse<TaskItemStatus>(request.Status);
            if (newStatus != task.Status && !IsValidTransition(task.Status, newStatus))
                throw new InvalidOperationException($"Cannot transition from {task.Status} to {newStatus}. Valid transitions: {string.Join(", ", ValidTransitions[task.Status])}.");
            task.Status = newStatus;

            // Auto-complete all steps when status is set to Done
            if (newStatus == TaskItemStatus.Done)
            {
                foreach (var step in task.Steps.Where(s => !s.IsCompleted))
                {
                    step.IsCompleted = true;
                    step.CompletedAt = DateTime.UtcNow;
                    step.CompletedById = userId;
                }
            }
        }

        if (request.Title is not null)
            task.Title = request.Title;

        if (request.Description is not null)
            task.Description = request.Description;

        if (request.Priority is not null)
            task.Priority = Enum.Parse<TaskPriority>(request.Priority);

        if (request.DueDate.HasValue)
            task.DueDate = request.DueDate;

        if (request.AssigneeIds is not null)
        {
            var newAssignees = await _userManager.Users
                .Where(u => request.AssigneeIds.Contains(u.Id))
                .ToListAsync(cancellationToken);
            task.Assignees.Clear();
            foreach (var a in newAssignees)
                task.Assignees.Add(a);
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task '{TaskId}' updated by {UserId}", taskId, userId);

        // ── Notifications ──
        var actorName = await GetUserFullName(userId, cancellationToken);
        var recipients = await GetTaskRecipientIds(taskId, cancellationToken);

        // Status changed
        if (request.Status is not null && Enum.Parse<TaskItemStatus>(request.Status) != oldStatus)
        {
            await _notificationService.NotifyAsync(
                NotificationType.TaskStatusChanged, userId, actorName,
                taskId, task.Title,
                $"{actorName} changed status of \"{task.Title}\" to {request.Status}",
                recipients, cancellationToken);
        }

        // Task details edited (title, description, priority, due date)
        if (hasNonStatusEdit)
        {
            await _notificationService.NotifyAsync(
                NotificationType.TaskEdited, userId, actorName,
                taskId, task.Title,
                $"{actorName} updated \"{task.Title}\"",
                recipients, cancellationToken);
        }

        // Assignee changes
        if (request.AssigneeIds is not null)
        {
            var newAssigneeIds = request.AssigneeIds.ToHashSet();
            var added = newAssigneeIds.Except(oldAssigneeIds).ToList();
            var removed = oldAssigneeIds.Except(newAssigneeIds).ToList();

            if (added.Count > 0)
            {
                await _notificationService.NotifyAsync(
                    NotificationType.TaskAssigned, userId, actorName,
                    taskId, task.Title,
                    $"{actorName} assigned you to \"{task.Title}\"",
                    added, cancellationToken);
            }

            if (removed.Count > 0)
            {
                await _notificationService.NotifyAsync(
                    NotificationType.TaskUnassigned, userId, actorName,
                    taskId, task.Title,
                    $"{actorName} unassigned you from \"{task.Title}\"",
                    removed, cancellationToken);
            }
        }

        return await ReloadTaskDetailAsync(task.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid taskId, Guid userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
            throw new KeyNotFoundException("Task not found.");

        if (!task.Assignees.Any(a => a.CompanyId == companyId))
            throw new UnauthorizedAccessException("You do not have access to this task.");

        // Collect recipients before deletion
        var recipients = task.Assignees.Select(a => a.Id).ToList();
        if (!recipients.Contains(task.CreatedById))
            recipients.Add(task.CreatedById);
        var taskTitle = task.Title;

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Task '{TaskId}' deleted", taskId);

        var actorName = await GetUserFullName(userId, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.TaskDeleted, userId, actorName,
            null, null,
            $"{actorName} deleted task \"{taskTitle}\"",
            recipients, cancellationToken);
    }

    public async Task<TaskStepDto> AddStepAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CreateTaskStepRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var maxSortOrder = task.Steps.Any() ? task.Steps.Max(s => s.SortOrder) : -1;

        var step = new TaskStep
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Title = request.Title,
            Instructions = request.Instructions,
            SortOrder = maxSortOrder + 1,
            IsCompleted = false,
        };

        _dbContext.TaskSteps.Add(step);

        await _dbContext.Tasks
            .Where(t => t.Id == taskId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var actorName = await GetUserFullName(userId, cancellationToken);
        var recipients = await GetTaskRecipientIds(taskId, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.StepAdded, userId, actorName,
            taskId, task.Title,
            $"{actorName} added step \"{request.Title}\" to \"{task.Title}\"",
            recipients, cancellationToken);

        return MapToStepDto(step);
    }

    public async Task<TaskStepDto> UpdateStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, UpdateTaskStepRequest request, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var step = await _dbContext.TaskSteps
            .Include(s => s.CompletedBy)
            .FirstOrDefaultAsync(s => s.Id == stepId && s.TaskId == taskId, cancellationToken);

        if (step is null)
            throw new KeyNotFoundException("Task step not found.");

        if (request.Title is not null)
            step.Title = request.Title;

        if (request.Instructions is not null)
            step.Instructions = request.Instructions;

        await _dbContext.Tasks
            .Where(t => t.Id == taskId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToStepDto(step);
    }

    public async Task<string?> CompleteStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var step = await _dbContext.TaskSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.TaskId == taskId, cancellationToken);

        if (step is null)
            throw new KeyNotFoundException("Task step not found.");

        step.IsCompleted = true;
        step.CompletedAt = DateTime.UtcNow;
        step.CompletedById = userId;

        string? newStatus = null;

        // If task is ToDo, move to InProgress when a step is completed
        if (task.Status == TaskItemStatus.ToDo)
        {
            task.Status = TaskItemStatus.InProgress;
            newStatus = task.Status.ToString();
        }

        // If all steps are now completed, move to Done
        var allSteps = await _dbContext.TaskSteps
            .Where(s => s.TaskId == taskId)
            .ToListAsync(cancellationToken);

        if (allSteps.All(s => s.IsCompleted))
        {
            task.Status = TaskItemStatus.Done;
            newStatus = task.Status.ToString();
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var actorName = await GetUserFullName(userId, cancellationToken);
        var recipients = await GetTaskRecipientIds(taskId, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.StepCompleted, userId, actorName,
            taskId, task.Title,
            $"{actorName} completed step \"{step.Title}\" on \"{task.Title}\"",
            recipients, cancellationToken);

        return newStatus;
    }

    public async Task<string?> UncompleteStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var step = await _dbContext.TaskSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.TaskId == taskId, cancellationToken);

        if (step is null)
            throw new KeyNotFoundException("Task step not found.");

        step.IsCompleted = false;
        step.CompletedAt = null;
        step.CompletedById = null;

        string? newStatus = null;

        // If task was Done, move back to InProgress since a step is now incomplete
        if (task.Status == TaskItemStatus.Done)
        {
            task.Status = TaskItemStatus.InProgress;
            newStatus = task.Status.ToString();
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newStatus;
    }

    public async Task DeleteStepAsync(Guid taskId, Guid stepId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var step = await _dbContext.TaskSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.TaskId == taskId, cancellationToken);

        if (step is null)
            throw new KeyNotFoundException("Task step not found.");

        _dbContext.TaskSteps.Remove(step);

        // Re-order remaining steps
        var remainingSteps = await _dbContext.TaskSteps
            .Where(s => s.TaskId == taskId && s.Id != stepId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < remainingSteps.Count; i++)
            remainingSteps[i].SortOrder = i;

        await _dbContext.Tasks
            .Where(t => t.Id == taskId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderStepsAsync(Guid taskId, Guid userId, Guid companyId, string userRole, ReorderTaskStepsRequest request, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var steps = await _dbContext.TaskSteps
            .Where(s => s.TaskId == taskId)
            .ToListAsync(cancellationToken);

        if (steps.Count != request.StepIds.Count || !steps.All(s => request.StepIds.Contains(s.Id)))
            throw new InvalidOperationException("Step IDs do not match existing steps for this task.");

        for (var i = 0; i < request.StepIds.Count; i++)
        {
            var step = steps.First(s => s.Id == request.StepIds[i]);
            step.SortOrder = i;
        }

        await _dbContext.Tasks
            .Where(t => t.Id == taskId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ── Comments ────────────────────────────────────────────────────────────

    public async Task<List<TaskCommentDto>> GetCommentsAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var comments = await _dbContext.TaskComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Attachments)
                .ThenInclude(a => a.UploadedBy)
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return comments.Select(MapToCommentDto).ToList();
    }

    public async Task<TaskCommentDto> AddCommentAsync(Guid taskId, Guid userId, Guid companyId, string userRole, CreateTaskCommentRequest request, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        var sanitizedContent = sanitizer.Sanitize(request.Content);

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorId = userId,
            Content = sanitizedContent,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.TaskComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment added to task '{TaskId}' by {UserId}", taskId, userId);

        // Notification
        var actorNameC = await GetUserFullName(userId, cancellationToken);
        var taskForNotif = await _dbContext.Tasks.AsNoTracking().FirstAsync(t => t.Id == taskId, cancellationToken);
        var recipientsC = await GetTaskRecipientIds(taskId, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.CommentAdded, userId, actorNameC,
            taskId, taskForNotif.Title,
            $"{actorNameC} commented on \"{taskForNotif.Title}\"",
            recipientsC, cancellationToken);

        // Reload with Author
        var saved = await _dbContext.TaskComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Attachments)
                .ThenInclude(a => a.UploadedBy)
            .FirstAsync(c => c.Id == comment.Id, cancellationToken);

        return MapToCommentDto(saved);
    }

    public async Task<TaskCommentDto> UpdateCommentAsync(Guid taskId, Guid commentId, Guid userId, Guid companyId, string userRole, UpdateTaskCommentRequest request, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId, cancellationToken);

        if (comment is null)
            throw new KeyNotFoundException("Comment not found.");

        if (comment.AuthorId != userId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");

        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        comment.Content = sanitizer.Sanitize(request.Content);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment '{CommentId}' updated by {UserId}", commentId, userId);

        // Notification
        var actorNameCE = await GetUserFullName(userId, cancellationToken);
        var taskForNotifCE = await _dbContext.Tasks.AsNoTracking().FirstAsync(t => t.Id == taskId, cancellationToken);
        var recipientsCE = await GetTaskRecipientIds(taskId, cancellationToken);
        await _notificationService.NotifyAsync(
            NotificationType.CommentEdited, userId, actorNameCE,
            taskId, taskForNotifCE.Title,
            $"{actorNameCE} edited a comment on \"{taskForNotifCE.Title}\"",
            recipientsCE, cancellationToken);

        var saved = await _dbContext.TaskComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Attachments)
                .ThenInclude(a => a.UploadedBy)
            .FirstAsync(c => c.Id == comment.Id, cancellationToken);

        return MapToCommentDto(saved);
    }

    public async Task DeleteCommentAsync(Guid taskId, Guid commentId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var comment = await _dbContext.TaskComments
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId, cancellationToken);

        if (comment is null)
            throw new KeyNotFoundException("Comment not found.");

        if (comment.AuthorId != userId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        // Delete physical files for comment attachments
        foreach (var attachment in comment.Attachments)
        {
            try { await _fileStorageService.DeleteFileAsync(attachment.StoredPath, cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete attachment file '{Path}'", attachment.StoredPath); }
        }

        _dbContext.TaskAttachments.RemoveRange(comment.Attachments);
        _dbContext.TaskComments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment '{CommentId}' deleted by {UserId}", commentId, userId);
    }

    // ── Attachments ─────────────────────────────────────────────────────────

    public async Task<TaskAttachmentDto> AddAttachmentAsync(Guid taskId, Guid? commentId, Guid userId, Guid companyId, string userRole, string fileName, string contentType, long fileSize, Stream fileStream, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        if (commentId.HasValue)
        {
            var commentExists = await _dbContext.TaskComments
                .AnyAsync(c => c.Id == commentId.Value && c.TaskId == taskId, cancellationToken);
            if (!commentExists)
                throw new KeyNotFoundException("Comment not found.");
        }

        var storedPath = await _fileStorageService.SaveFileAsync(taskId, fileName, fileStream, cancellationToken);

        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            CommentId = commentId,
            FileName = fileName,
            StoredPath = storedPath,
            ContentType = contentType,
            FileSize = fileSize,
            UploadedById = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.TaskAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment '{FileName}' uploaded to task '{TaskId}' by {UserId}", fileName, taskId, userId);

        // Notification for task-level attachments
        if (!commentId.HasValue)
        {
            var actorNameA = await GetUserFullName(userId, cancellationToken);
            var taskForNotifA = await _dbContext.Tasks.AsNoTracking().FirstAsync(t => t.Id == taskId, cancellationToken);
            var recipientsA = await GetTaskRecipientIds(taskId, cancellationToken);
            await _notificationService.NotifyAsync(
                NotificationType.AttachmentAdded, userId, actorNameA,
                taskId, taskForNotifA.Title,
                $"{actorNameA} added attachment \"{fileName}\" to \"{taskForNotifA.Title}\"",
                recipientsA, cancellationToken);
        }

        var saved = await _dbContext.TaskAttachments
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstAsync(a => a.Id == attachment.Id, cancellationToken);

        return MapToAttachmentDto(saved);
    }

    public async Task DeleteAttachmentAsync(Guid taskId, Guid attachmentId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var attachment = await _dbContext.TaskAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == taskId, cancellationToken);

        if (attachment is null)
            throw new KeyNotFoundException("Attachment not found.");

        if (attachment.UploadedById != userId)
            throw new UnauthorizedAccessException("You can only delete your own attachments.");

        try { await _fileStorageService.DeleteFileAsync(attachment.StoredPath, cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete attachment file '{Path}'", attachment.StoredPath); }

        _dbContext.TaskAttachments.Remove(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment '{AttachmentId}' deleted by {UserId}", attachmentId, userId);
    }

    public async Task<(Stream Stream, string FileName, string ContentType)> DownloadAttachmentAsync(Guid taskId, Guid attachmentId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken = default)
    {
        await GetTaskWithAccessCheck(taskId, userId, companyId, userRole, cancellationToken);

        var attachment = await _dbContext.TaskAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TaskId == taskId, cancellationToken);

        if (attachment is null)
            throw new KeyNotFoundException("Attachment not found.");

        var stream = await _fileStorageService.GetFileAsync(attachment.StoredPath, cancellationToken);
        return (stream, attachment.FileName, attachment.ContentType);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<TaskItem> GetTaskWithAccessCheck(Guid taskId, Guid userId, Guid companyId, string userRole, CancellationToken cancellationToken)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.Assignees)
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task is null)
            throw new KeyNotFoundException("Task not found.");

        if (!HasAccess(task, userId, companyId, userRole))
            throw new UnauthorizedAccessException("You do not have access to this task.");

        return task;
    }

    private static bool HasAccess(TaskItem task, Guid userId, Guid companyId, string userRole)
    {
        if (userRole == "Admin")
            return true;

        if (userRole == "Manager")
            return task.Assignees.Any(a => a.CompanyId == companyId) || task.CreatedById == userId;

        return task.Assignees.Any(a => a.Id == userId) || task.CreatedById == userId;
    }

    private static bool IsValidTransition(TaskItemStatus from, TaskItemStatus to)
    {
        return ValidTransitions.TryGetValue(from, out var validTargets) && validTargets.Contains(to);
    }

    /// <summary>Gets all assignee IDs + the task creator ID as notification recipients.</summary>
    private async Task<List<Guid>> GetTaskRecipientIds(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.Assignees)
            .FirstAsync(t => t.Id == taskId, cancellationToken);

        var ids = task.Assignees.Select(a => a.Id).ToList();
        if (!ids.Contains(task.CreatedById))
            ids.Add(task.CreatedById);
        return ids;
    }

    private async Task<string> GetUserFullName(Guid userId, CancellationToken cancellationToken)
    {
        var u = await _userManager.Users.AsNoTracking().FirstAsync(x => x.Id == userId, cancellationToken);
        return u.FirstName + " " + u.LastName;
    }

    private async Task ValidateAssigneeInCompany(Guid assigneeId, Guid companyId, CancellationToken cancellationToken)
    {
        var assignee = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == assigneeId && u.CompanyId == companyId && u.IsActive, cancellationToken);

        if (assignee is null)
            throw new InvalidOperationException("Assignee not found in your company.");
    }

    private async Task<TaskDetailDto> ReloadTaskDetailAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _dbContext.Tasks
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Assignees)
            .Include(t => t.SourceTemplate)
            .Include(t => t.Steps.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.CompletedBy)
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .Include(t => t.Comments)
                .ThenInclude(c => c.Attachments)
                    .ThenInclude(a => a.UploadedBy)
            .Include(t => t.Attachments.Where(a => a.CommentId == null))
                .ThenInclude(a => a.UploadedBy)
            .FirstAsync(t => t.Id == taskId, cancellationToken);

        return MapToDetailDto(task);
    }

    private static TaskItemDto MapToItemDto(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status.ToString(),
        Priority = task.Priority.ToString(),
        DueDate = task.DueDate,
        CreatedById = task.CreatedById,
        CreatedByName = task.CreatedBy.FirstName + " " + task.CreatedBy.LastName,
        Assignees = task.Assignees.Select(a => new TaskAssigneeDto { Id = a.Id, Name = a.FirstName + " " + a.LastName }).ToList(),
        SourceTemplateId = task.SourceTemplateId,
        SourceTemplateName = task.SourceTemplate?.Name,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        StepCount = task.Steps.Count,
        CompletedStepCount = task.Steps.Count(s => s.IsCompleted),
    };

    private static TaskDetailDto MapToDetailDto(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status.ToString(),
        Priority = task.Priority.ToString(),
        DueDate = task.DueDate,
        CreatedById = task.CreatedById,
        CreatedByName = task.CreatedBy.FirstName + " " + task.CreatedBy.LastName,
        Assignees = task.Assignees.Select(a => new TaskAssigneeDto { Id = a.Id, Name = a.FirstName + " " + a.LastName }).ToList(),
        SourceTemplateId = task.SourceTemplateId,
        SourceTemplateName = task.SourceTemplate?.Name,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        StepCount = task.Steps.Count,
        CompletedStepCount = task.Steps.Count(s => s.IsCompleted),
        Steps = task.Steps.OrderBy(s => s.SortOrder).Select(MapToStepDto).ToList(),
        Comments = task.Comments.OrderBy(c => c.CreatedAt).Select(MapToCommentDto).ToList(),
        Attachments = task.Attachments.Where(a => a.CommentId == null).Select(MapToAttachmentDto).ToList(),
    };

    private static TaskStepDto MapToStepDto(TaskStep step) => new()
    {
        Id = step.Id,
        TaskId = step.TaskId,
        Title = step.Title,
        Instructions = step.Instructions,
        SortOrder = step.SortOrder,
        IsCompleted = step.IsCompleted,
        CompletedAt = step.CompletedAt,
        CompletedById = step.CompletedById,
        CompletedByName = step.CompletedBy is not null ? step.CompletedBy.FirstName + " " + step.CompletedBy.LastName : null,
    };

    private static TaskCommentDto MapToCommentDto(TaskComment comment) => new()
    {
        Id = comment.Id,
        TaskId = comment.TaskId,
        AuthorId = comment.AuthorId,
        AuthorName = comment.Author.FirstName + " " + comment.Author.LastName,
        Content = comment.Content,
        CreatedAt = comment.CreatedAt,
        Attachments = comment.Attachments.Select(MapToAttachmentDto).ToList(),
    };

    private static TaskAttachmentDto MapToAttachmentDto(TaskAttachment attachment) => new()
    {
        Id = attachment.Id,
        TaskId = attachment.TaskId,
        CommentId = attachment.CommentId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSize = attachment.FileSize,
        UploadedById = attachment.UploadedById,
        UploadedByName = attachment.UploadedBy.FirstName + " " + attachment.UploadedBy.LastName,
        CreatedAt = attachment.CreatedAt,
    };
}
