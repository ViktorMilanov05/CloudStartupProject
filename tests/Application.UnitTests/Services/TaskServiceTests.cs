using Application.DTOs.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.UnitTests.Services;

public class TaskServiceTests
{
    private readonly Mock<ILogger<TaskService>> _loggerMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private (TaskService service, AppDbContext db, UserManager<User> userManager) CreateService(string? dbName = null)
    {
        var db = TestHelpers.CreateInMemoryDbContext(dbName);
        var userManager = CreateMockUserManager(db);
        var service = new TaskService(db, userManager, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);
        return (service, db, userManager);
    }

    private static UserManager<User> CreateMockUserManager(AppDbContext db)
    {
        var store = new Mock<IUserStore<User>>();
        var mgr = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Wire up Users property to the in-memory db
        mgr.Setup(m => m.Users).Returns(db.Users);

        return mgr.Object;
    }

    private async Task<(AppDbContext db, Company company, User manager, User user)> SetupDbWithCompanyAndUsers(string? dbName = null)
    {
        var db = TestHelpers.CreateInMemoryDbContext(dbName);
        var company = TestHelpers.CreateTestCompany();
        var manager = TestHelpers.CreateTestUser(company.Id, UserRole.Manager);
        var user = TestHelpers.CreateTestUser(company.Id, UserRole.User);

        db.Companies.Add(company);
        db.Users.Add(manager);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (db, company, manager, user);
    }

    // ── Create Task ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ManagerCanAssignMultipleUsers()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var request = new CreateTaskRequest
        {
            Title = "Multi-assign task",
            Priority = "High",
            AssigneeIds = [manager.Id, user.Id]
        };

        var result = await service.CreateAsync(manager.Id, company.Id, "Manager", request);

        result.Title.Should().Be("Multi-assign task");
        result.Assignees.Should().HaveCount(2);
        result.Status.Should().Be("ToDo");
        result.Priority.Should().Be("High");
    }

    [Fact]
    public async Task CreateAsync_UserCanOnlyAssignToSelf()
    {
        var (db, company, _, user) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var request = new CreateTaskRequest
        {
            Title = "Self-assign",
            Priority = "Medium",
            AssigneeIds = [user.Id]
        };

        var result = await service.CreateAsync(user.Id, company.Id, "User", request);

        result.Assignees.Should().HaveCount(1);
        result.Assignees[0].Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateAsync_UserCannotAssignOthers()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var request = new CreateTaskRequest
        {
            Title = "Invalid assign",
            Priority = "Medium",
            AssigneeIds = [manager.Id]
        };

        var act = () => service.CreateAsync(user.Id, company.Id, "User", request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Users can only assign tasks to themselves*");
    }

    [Fact]
    public async Task CreateAsync_SendsNotificationToAssignees()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var request = new CreateTaskRequest
        {
            Title = "Notif task",
            Priority = "Medium",
            AssigneeIds = [user.Id]
        };

        await service.CreateAsync(manager.Id, company.Id, "Manager", request);

        _notificationServiceMock.Verify(
            n => n.NotifyAsync(
                NotificationType.TaskAssigned,
                manager.Id,
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                "Notif task",
                It.IsAny<string>(),
                It.Is<List<Guid>>(ids => ids.Contains(user.Id)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Create From Template ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateFromTemplateAsync_SnapshotsSteps()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var template = TestHelpers.CreateTestTemplate(manager.Id, stepCount: 3);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var request = new CreateTaskFromTemplateRequest
        {
            Title = "From Template",
            Priority = "Medium",
            AssigneeIds = [user.Id]
        };

        var result = await service.CreateFromTemplateAsync(template.Id, manager.Id, company.Id, "Manager", request);

        result.Steps.Should().HaveCount(3);
        result.SourceTemplateId.Should().Be(template.Id);
        result.Steps[0].Title.Should().Be("Step 1");
        result.Steps[1].Title.Should().Be("Step 2");
        result.Steps[2].Title.Should().Be("Step 3");
    }

    [Fact]
    public async Task CreateFromTemplateAsync_ThrowsIfTemplateNotFound()
    {
        var (db, company, manager, _) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var request = new CreateTaskFromTemplateRequest
        {
            Priority = "Medium",
            AssigneeIds = [manager.Id]
        };

        var act = () => service.CreateFromTemplateAsync(Guid.NewGuid(), manager.Id, company.Id, "Manager", request);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Get / Visibility ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_UserSeesOnlyAssignedTasks()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var user2 = TestHelpers.CreateTestUser(company.Id, UserRole.User);
        db.Users.Add(user2);

        // Task assigned to user
        var task1 = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "My Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        // Task assigned to user2
        var task2 = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Other Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user2 },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.AddRange(task1, task2);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(user.Id, company.Id, "User", new TaskFilterRequest());

        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("My Task");
    }

    [Fact]
    public async Task GetAllAsync_ManagerSeesAllCompanyTasks()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();

        var task1 = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task 1", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var task2 = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task 2", Status = TaskItemStatus.InProgress,
            Priority = TaskPriority.High, CreatedById = manager.Id,
            Assignees = new List<User> { manager },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.AddRange(task1, task2);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest());

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_FiltersWork()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();

        db.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "ToDo Task", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Low, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "InProgress Task", Status = TaskItemStatus.InProgress, Priority = TaskPriority.High, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { Status = "ToDo" });
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("ToDo Task");

        var result2 = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { Priority = "High" });
        result2.Items.Should().HaveCount(1);
        result2.Items[0].Title.Should().Be("InProgress Task");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        var (db, company, manager, _) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetByIdAsync(Guid.NewGuid(), manager.Id, company.Id, "Manager");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNoAccess()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();

        var otherCompany = TestHelpers.CreateTestCompany();
        var otherUser = TestHelpers.CreateTestUser(otherCompany.Id, UserRole.User);
        db.Companies.Add(otherCompany);
        db.Users.Add(otherUser);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Company Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetByIdAsync(task.Id, otherUser.Id, otherCompany.Id, "User");
        result.Should().BeNull();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesTitle()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Old Title", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Title = "New Title" });
        result.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task UpdateAsync_ValidStatusTransition_Succeeds()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Status = "InProgress" });
        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task UpdateAsync_InvalidStatusTransition_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Status = "Done" });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cannot transition*");
    }

    [Fact]
    public async Task UpdateAsync_UserCannotReassign()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateAsync(task.Id, user.Id, company.Id, "User", new UpdateTaskRequest { AssigneeIds = [manager.Id] });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Users cannot reassign*");
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenNotFound()
    {
        var (db, company, manager, _) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateAsync(Guid.NewGuid(), manager.Id, company.Id, "Manager", new UpdateTaskRequest { Title = "X" });
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "To Delete", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.DeleteAsync(task.Id, manager.Id, company.Id);

        var exists = await db.Tasks.AnyAsync(t => t.Id == task.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenNotFound()
    {
        var (db, company, manager, _) = await SetupDbWithCompanyAndUsers();
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.DeleteAsync(Guid.NewGuid(), manager.Id, company.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    [Fact(Skip = "ExecuteUpdateAsync not supported by InMemory provider — tested via integration tests")]
    public async Task AddStepAsync_AddsToTask()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var step = await service.AddStepAsync(task.Id, manager.Id, company.Id, "Manager", new CreateTaskStepRequest { Title = "New Step", Instructions = "Do this" });

        step.Title.Should().Be("New Step");
        step.Instructions.Should().Be("Do this");
        step.SortOrder.Should().Be(0);
        step.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteStepAsync_MarksStepDone()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var step = new TaskStep { Id = Guid.NewGuid(), TaskId = task.Id, Title = "Step", SortOrder = 0, IsCompleted = false };
        task.Steps.Add(step);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var newStatus = await service.CompleteStepAsync(task.Id, step.Id, user.Id, company.Id, "User");

        var updatedStep = await db.TaskSteps.FirstAsync(s => s.Id == step.Id);
        updatedStep.IsCompleted.Should().BeTrue();
        updatedStep.CompletedById.Should().Be(user.Id);
        // Task should move from ToDo to InProgress, then to Done since all steps completed
        newStatus.Should().Be("Done");
    }

    [Fact]
    public async Task UncompleteStepAsync_MarksStepIncomplete()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.Done,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var step = new TaskStep { Id = Guid.NewGuid(), TaskId = task.Id, Title = "Step", SortOrder = 0, IsCompleted = true, CompletedAt = DateTime.UtcNow, CompletedById = user.Id };
        task.Steps.Add(step);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var newStatus = await service.UncompleteStepAsync(task.Id, step.Id, user.Id, company.Id, "User");

        var updatedStep = await db.TaskSteps.FirstAsync(s => s.Id == step.Id);
        updatedStep.IsCompleted.Should().BeFalse();
        updatedStep.CompletedById.Should().BeNull();
        newStatus.Should().Be("InProgress");
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by InMemory provider — tested via integration tests")]
    public async Task DeleteStepAsync_RemovesStep()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var step = new TaskStep { Id = Guid.NewGuid(), TaskId = task.Id, Title = "Step", SortOrder = 0 };
        task.Steps.Add(step);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.DeleteStepAsync(task.Id, step.Id, manager.Id, company.Id, "Manager");

        var exists = await db.TaskSteps.AnyAsync(s => s.Id == step.Id);
        exists.Should().BeFalse();
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddCommentAsync_SanitizesHtml()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var comment = await service.AddCommentAsync(task.Id, user.Id, company.Id, "User",
            new CreateTaskCommentRequest { Content = "<p>Hello</p><script>alert('xss')</script>" });

        comment.Content.Should().NotContain("<script>");
        comment.Content.Should().Contain("<p>Hello</p>");
    }

    [Fact]
    public async Task UpdateCommentAsync_OnlyAuthorCanEdit()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user, manager },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id,
            Content = "Original", CreatedAt = DateTime.UtcNow
        };
        task.Comments.Add(comment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateCommentAsync(task.Id, comment.Id, manager.Id, company.Id, "Manager",
            new UpdateTaskCommentRequest { Content = "Hacked" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteCommentAsync_OnlyAuthorCanDelete()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user, manager },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id,
            Content = "My comment", CreatedAt = DateTime.UtcNow
        };
        task.Comments.Add(comment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.DeleteCommentAsync(task.Id, comment.Id, manager.Id, company.Id, "Manager");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Attachments ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAttachmentAsync_SavesAndReturnsDto()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        _fileStorageMock.Setup(f => f.SaveFileAsync(task.Id, "test.pdf", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/test.pdf");

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        using var stream = new MemoryStream(new byte[100]);
        var attachment = await service.AddAttachmentAsync(task.Id, null, user.Id, company.Id, "User",
            "test.pdf", "application/pdf", 100, stream);

        attachment.FileName.Should().Be("test.pdf");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.FileSize.Should().Be(100);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_OnlyUploaderCanDelete()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user, manager },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, FileName = "file.pdf",
            StoredPath = "/path/file.pdf", ContentType = "application/pdf", FileSize = 100,
            UploadedById = user.Id, CreatedAt = DateTime.UtcNow
        };
        task.Attachments.Add(attachment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.DeleteAttachmentAsync(task.Id, attachment.Id, manager.Id, company.Id, "Manager");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Additional Update Paths ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesDescription()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Description = "Old", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Description = "New Desc" });
        result.Description.Should().Be("New Desc");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesPriority()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Priority = "High" });
        result.Priority.Should().Be("High");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDueDate()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var dueDate = DateTime.UtcNow.AddDays(10);
        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { DueDate = dueDate });
        result.DueDate.Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateAsync_ManagerReassigns_NotifiesAddedAndRemoved()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var user2 = TestHelpers.CreateTestUser(company.Id);
        db.Users.Add(user2);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Reassign Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        // Reassign from user to user2
        var result = await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { AssigneeIds = [user2.Id] });

        result.Assignees.Should().HaveCount(1);
        result.Assignees[0].Id.Should().Be(user2.Id);

        // Verify assigned notification sent to user2
        _notificationServiceMock.Verify(
            n => n.NotifyAsync(NotificationType.TaskAssigned, manager.Id, It.IsAny<string>(),
                task.Id, "Reassign Task", It.IsAny<string>(),
                It.Is<List<Guid>>(ids => ids.Contains(user2.Id)), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify unassigned notification sent to user
        _notificationServiceMock.Verify(
            n => n.NotifyAsync(NotificationType.TaskUnassigned, manager.Id, It.IsAny<string>(),
                task.Id, "Reassign Task", It.IsAny<string>(),
                It.Is<List<Guid>>(ids => ids.Contains(user.Id)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_StatusChangeNotifiesRecipients()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Status Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Status = "InProgress" });

        _notificationServiceMock.Verify(
            n => n.NotifyAsync(NotificationType.TaskStatusChanged, manager.Id, It.IsAny<string>(),
                task.Id, "Status Task", It.IsAny<string>(),
                It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NoAccessThrows()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var otherCompany = TestHelpers.CreateTestCompany();
        var otherUser = TestHelpers.CreateTestUser(otherCompany.Id);
        db.Companies.Add(otherCompany);
        db.Users.Add(otherUser);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Access Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateAsync(task.Id, otherUser.Id, otherCompany.Id, "User", new UpdateTaskRequest { Title = "Hack" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Additional Filter Tests ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_SearchFilter()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        db.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "ABC Important Task", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "XYZ Other", Description = "Important thing", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Low, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "No Match", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Low, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { Search = "Important" });
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_AssigneeFilter()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var user2 = TestHelpers.CreateTestUser(company.Id);
        db.Users.Add(user2);

        db.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "User1 Task", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "User2 Task", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user2 }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { AssigneeId = user2.Id });
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("User2 Task");
    }

    [Fact]
    public async Task GetAllAsync_DueDateRangeFilter()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var now = DateTime.UtcNow;

        db.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "Past Task", DueDate = now.AddDays(-5), Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = now, UpdatedAt = now },
            new TaskItem { Id = Guid.NewGuid(), Title = "Future Task", DueDate = now.AddDays(10), Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = now, UpdatedAt = now },
            new TaskItem { Id = Guid.NewGuid(), Title = "No DueDate", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { DueDateFrom = now.AddDays(-1), DueDateTo = now.AddDays(15) });
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Future Task");
    }

    [Fact]
    public async Task GetAllAsync_SortByTitle()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        db.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "Zulu", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "Alpha", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Low, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { SortBy = "title" });
        result.Items[0].Title.Should().Be("Alpha");
        result.Items[1].Title.Should().Be("Zulu");
    }

    [Fact]
    public async Task GetAllAsync_SortByTitleDescending()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        db.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "Alpha", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Low, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "Zulu", Status = TaskItemStatus.ToDo, Priority = TaskPriority.Medium, CreatedById = manager.Id, Assignees = new List<User> { user }, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetAllAsync(manager.Id, company.Id, "Manager", new TaskFilterRequest { SortBy = "title", SortDescending = true });
        result.Items[0].Title.Should().Be("Zulu");
        result.Items[1].Title.Should().Be("Alpha");
    }

    // ── GetCommentsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommentsAsync_ReturnsSortedComments()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        task.Comments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id, Content = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-10) });
        task.Comments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = manager.Id, Content = "Second", CreatedAt = DateTime.UtcNow });
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var comments = await service.GetCommentsAsync(task.Id, manager.Id, company.Id, "Manager");
        comments.Should().HaveCount(2);
        comments[0].Content.Should().Be("First");
        comments[1].Content.Should().Be("Second");
    }

    // ── DeleteCommentAsync (author deletes own) ─────────────────────────────

    [Fact]
    public async Task DeleteCommentAsync_AuthorCanDelete()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user, manager },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id,
            Content = "My comment", CreatedAt = DateTime.UtcNow
        };
        task.Comments.Add(comment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.DeleteCommentAsync(task.Id, comment.Id, user.Id, company.Id, "User");

        var exists = await db.TaskComments.AnyAsync(c => c.Id == comment.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCommentAsync_CommentNotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.DeleteCommentAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DownloadAttachmentAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DownloadAttachmentAsync_ReturnsStream()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, FileName = "doc.pdf",
            StoredPath = "/path/doc.pdf", ContentType = "application/pdf", FileSize = 100,
            UploadedById = user.Id, CreatedAt = DateTime.UtcNow
        };
        task.Attachments.Add(attachment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        _fileStorageMock.Setup(f => f.GetFileAsync("/path/doc.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var (stream, fileName, contentType) = await service.DownloadAttachmentAsync(task.Id, attachment.Id, user.Id, company.Id, "User");

        fileName.Should().Be("doc.pdf");
        contentType.Should().Be("application/pdf");
        stream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadAttachmentAsync_NotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.DownloadAttachmentAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteAttachmentAsync (owner deletes own) ───────────────────────────

    [Fact]
    public async Task DeleteAttachmentAsync_OwnerCanDelete()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, FileName = "mine.pdf",
            StoredPath = "/path/mine.pdf", ContentType = "application/pdf", FileSize = 50,
            UploadedById = user.Id, CreatedAt = DateTime.UtcNow
        };
        task.Attachments.Add(attachment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.DeleteAttachmentAsync(task.Id, attachment.Id, user.Id, company.Id, "User");

        var exists = await db.TaskAttachments.AnyAsync(a => a.Id == attachment.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAttachmentAsync_NotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.DeleteAttachmentAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CreateAsync edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ManagerValidatesAssigneeInCompany()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var otherCompany = TestHelpers.CreateTestCompany();
        var outsider = TestHelpers.CreateTestUser(otherCompany.Id);
        db.Companies.Add(otherCompany);
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.CreateAsync(manager.Id, company.Id, "Manager", new CreateTaskRequest
        {
            Title = "Bad Assign", Priority = "Medium", AssigneeIds = [outsider.Id]
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Assignee not found in your company*");
    }

    // ── UpdateComment (author, sanitize) ────────────────────────────────────

    [Fact]
    public async Task UpdateCommentAsync_AuthorCanEdit_SanitizesHtml()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user, manager },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id,
            Content = "Original", CreatedAt = DateTime.UtcNow
        };
        task.Comments.Add(comment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.UpdateCommentAsync(task.Id, comment.Id, user.Id, company.Id, "User",
            new UpdateTaskCommentRequest { Content = "<p>Updated</p><script>alert(1)</script>" });

        result.Content.Should().Contain("<p>Updated</p>");
        result.Content.Should().NotContain("<script>");
    }

    [Fact]
    public async Task UpdateCommentAsync_CommentNotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateCommentAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User",
            new UpdateTaskCommentRequest { Content = "X" });
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CompleteStep edge cases ─────────────────────────────────────────────

    [Fact]
    public async Task CompleteStepAsync_StepNotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.CompleteStepAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UncompleteStepAsync_StepNotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UncompleteStepAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CompleteStep partial (not all done) ──────────────────────────────

    [Fact]
    public async Task CompleteStepAsync_NotAllStepsDone_MovesToInProgressNotDone()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var step1 = new TaskStep { Id = Guid.NewGuid(), TaskId = task.Id, Title = "Step 1", SortOrder = 0, IsCompleted = false };
        var step2 = new TaskStep { Id = Guid.NewGuid(), TaskId = task.Id, Title = "Step 2", SortOrder = 1, IsCompleted = false };
        task.Steps.Add(step1);
        task.Steps.Add(step2);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var newStatus = await service.CompleteStepAsync(task.Id, step1.Id, user.Id, company.Id, "User");

        // Only one step completed, should be InProgress not Done
        newStatus.Should().Be("InProgress");
    }

    // ── Delete notifies recipients ──────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_NotifiesRecipients()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "To Delete", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.DeleteAsync(task.Id, manager.Id, company.Id);

        _notificationServiceMock.Verify(
            n => n.NotifyAsync(NotificationType.TaskDeleted, manager.Id, It.IsAny<string>(),
                null, null, It.IsAny<string>(),
                It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── AddAttachment with commentId ────────────────────────────────────────

    [Fact]
    public async Task AddAttachmentAsync_WithCommentId_LinksToComment()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id,
            Content = "A comment", CreatedAt = DateTime.UtcNow
        };
        task.Comments.Add(comment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        _fileStorageMock.Setup(f => f.SaveFileAsync(task.Id, "img.png", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/img.png");

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        using var stream = new MemoryStream(new byte[50]);
        var attachment = await service.AddAttachmentAsync(task.Id, comment.Id, user.Id, company.Id, "User",
            "img.png", "image/png", 50, stream);

        attachment.FileName.Should().Be("img.png");
        // Should NOT send attachment notification for comment-level attachments
        _notificationServiceMock.Verify(
            n => n.NotifyAsync(NotificationType.AttachmentAdded, It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddAttachmentAsync_CommentNotFound_Throws()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        using var stream = new MemoryStream(new byte[10]);
        var act = () => service.AddAttachmentAsync(task.Id, Guid.NewGuid(), user.Id, company.Id, "User",
            "file.pdf", "application/pdf", 10, stream);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteComment with attachments ──────────────────────────────────────

    [Fact]
    public async Task DeleteCommentAsync_DeletesPhysicalAttachmentFiles()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, AuthorId = user.Id,
            Content = "With attachment", CreatedAt = DateTime.UtcNow
        };
        var att = new TaskAttachment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, CommentId = comment.Id,
            FileName = "file.pdf", StoredPath = "/path/file.pdf",
            ContentType = "application/pdf", FileSize = 100,
            UploadedById = user.Id, CreatedAt = DateTime.UtcNow
        };
        comment.Attachments.Add(att);
        task.Comments.Add(comment);
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        await service.DeleteCommentAsync(task.Id, comment.Id, user.Id, company.Id, "User");

        _fileStorageMock.Verify(f => f.DeleteFileAsync("/path/file.pdf", It.IsAny<CancellationToken>()), Times.Once);
        (await db.TaskComments.AnyAsync(c => c.Id == comment.Id)).Should().BeFalse();
        (await db.TaskAttachments.AnyAsync(a => a.Id == att.Id)).Should().BeFalse();
    }

    // ── HasAccess - Creator has access ──────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_CreatorHasAccessEvenIfNotAssigned()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Creator Task", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user }, // manager not in assignees
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetByIdAsync(task.Id, manager.Id, company.Id, "Manager");
        result.Should().NotBeNull();
        result!.Title.Should().Be("Creator Task");
    }

    // ── HasAccess Security Fix Verification ──────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_CreatorCanAccessEvenIfNotAssigned()
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Creator Access", Status = TaskItemStatus.ToDo,
            Priority = TaskPriority.Medium, CreatedById = user.Id,
            Assignees = new List<User> { manager }, // user is creator but NOT assignee
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var result = await service.GetByIdAsync(task.Id, user.Id, company.Id, "User");
        result.Should().NotBeNull();
        result!.Title.Should().Be("Creator Access");
    }

    // ── Status transition matrix ─────────────────────────────────────────────

    [Theory]
    [InlineData("ToDo", "InProgress", true)]
    [InlineData("ToDo", "Blocked", true)]
    [InlineData("ToDo", "Done", false)]
    [InlineData("InProgress", "ToDo", true)]
    [InlineData("InProgress", "Done", true)]
    [InlineData("InProgress", "Blocked", true)]
    [InlineData("Blocked", "InProgress", true)]
    [InlineData("Blocked", "ToDo", true)]
    [InlineData("Blocked", "Done", false)]
    [InlineData("Done", "InProgress", true)]
    [InlineData("Done", "ToDo", false)]
    public async Task UpdateAsync_StatusTransitions(string from, string to, bool shouldSucceed)
    {
        var (db, company, manager, user) = await SetupDbWithCompanyAndUsers();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Title = "Task", Status = Enum.Parse<TaskItemStatus>(from),
            Priority = TaskPriority.Medium, CreatedById = manager.Id,
            Assignees = new List<User> { user },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var userMgr = CreateMockUserManager(db);
        var service = new TaskService(db, userMgr, _loggerMock.Object, _fileStorageMock.Object, _notificationServiceMock.Object);

        var act = () => service.UpdateAsync(task.Id, manager.Id, company.Id, "Manager", new UpdateTaskRequest { Status = to });

        if (shouldSucceed)
        {
            var result = await act();
            result.Status.Should().Be(to);
        }
        else
        {
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
