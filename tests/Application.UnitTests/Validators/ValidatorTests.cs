using Application.DTOs.Auth;
using Application.DTOs.Tasks;
using Application.DTOs.Templates;
using Application.Validators.Auth;
using Application.Validators.Tasks;
using Application.Validators.Templates;
using FluentAssertions;

namespace Application.UnitTests.Validators;

public class ValidatorTests
{
    // ── Auth Validators ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoginRequestValidator_RejectsEmptyEmail()
    {
        var validator = new LoginRequestValidator();
        var result = await validator.ValidateAsync(new LoginRequest { Email = "", Password = "pass" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task LoginRequestValidator_RejectsInvalidEmail()
    {
        var validator = new LoginRequestValidator();
        var result = await validator.ValidateAsync(new LoginRequest { Email = "notanemail", Password = "pass" });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SetupRequestValidator_RejectsShortPassword()
    {
        var validator = new SetupRequestValidator();
        var result = await validator.ValidateAsync(new SetupRequest
        {
            Email = "admin@test.com",
            Password = "short",
            FirstName = "Test",
            LastName = "Admin"
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task SetupRequestValidator_AcceptsValidRequest()
    {
        var validator = new SetupRequestValidator();
        var result = await validator.ValidateAsync(new SetupRequest
        {
            Email = "admin@test.com",
            Password = "ValidPass123",
            FirstName = "Test",
            LastName = "Admin"
        });
        result.IsValid.Should().BeTrue();
    }

    // ── Task Validators ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskRequestValidator_RejectsEmptyTitle()
    {
        var validator = new CreateTaskRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskRequest
        {
            Title = "",
            Priority = "Medium",
            AssigneeIds = [Guid.NewGuid()]
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public async Task CreateTaskRequestValidator_RejectsTitleOver300()
    {
        var validator = new CreateTaskRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskRequest
        {
            Title = new string('A', 301),
            Priority = "Medium",
            AssigneeIds = [Guid.NewGuid()]
        });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTaskRequestValidator_RejectsInvalidPriority()
    {
        var validator = new CreateTaskRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskRequest
        {
            Title = "Valid Title",
            Priority = "SuperUrgent",
            AssigneeIds = [Guid.NewGuid()]
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Priority");
    }

    [Fact]
    public async Task CreateTaskRequestValidator_RejectsEmptyAssignees()
    {
        var validator = new CreateTaskRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskRequest
        {
            Title = "Valid Title",
            Priority = "Medium",
            AssigneeIds = []
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssigneeIds");
    }

    [Fact]
    public async Task CreateTaskCommentRequestValidator_RejectsOver10000()
    {
        var validator = new CreateTaskCommentRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskCommentRequest
        {
            Content = new string('X', 10001)
        });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTaskCommentRequestValidator_AcceptsValidContent()
    {
        var validator = new CreateTaskCommentRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskCommentRequest
        {
            Content = "<p>This is a valid comment</p>"
        });
        result.IsValid.Should().BeTrue();
    }

    // ── Template Validators ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplateRequestValidator_RejectsNoSteps()
    {
        var validator = new CreateTemplateRequestValidator();
        var result = await validator.ValidateAsync(new CreateTemplateRequest
        {
            Name = "Template",
            Steps = []
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Steps");
    }

    [Fact]
    public async Task CreateTemplateRequestValidator_AcceptsValid()
    {
        var validator = new CreateTemplateRequestValidator();
        var result = await validator.ValidateAsync(new CreateTemplateRequest
        {
            Name = "Template",
            Steps = [new() { Title = "Step 1" }]
        });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderStepsRequestValidator_RejectsDuplicateIds()
    {
        var validator = new ReorderStepsRequestValidator();
        var id = Guid.NewGuid();
        var result = await validator.ValidateAsync(new ReorderStepsRequest
        {
            StepIds = [id, id]
        });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTaskStepRequestValidator_RejectsInstructionsOver4000()
    {
        var validator = new CreateTaskStepRequestValidator();
        var result = await validator.ValidateAsync(new CreateTaskStepRequest
        {
            Title = "Step",
            Instructions = new string('X', 4001)
        });
        result.IsValid.Should().BeFalse();
    }
}
