using Application.DTOs.Auth;
using Application.DTOs.Companies;
using Application.DTOs.Tasks;
using Application.DTOs.Templates;
using Application.DTOs.Users;
using Application.Validators.Auth;
using Application.Validators.Companies;
using Application.Validators.Tasks;
using Application.Validators.Templates;
using Application.Validators.Users;
using FluentAssertions;

namespace Application.UnitTests.Validators;

public class AllValidatorTests
{
    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidRequest_Passes()
    {
        var v = new LoginRequestValidator();
        var r = await v.ValidateAsync(new LoginRequest { Email = "test@test.com", Password = "password" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Login_EmptyEmail_Fails()
    {
        var v = new LoginRequestValidator();
        var r = await v.ValidateAsync(new LoginRequest { Email = "", Password = "password" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Login_InvalidEmail_Fails()
    {
        var v = new LoginRequestValidator();
        var r = await v.ValidateAsync(new LoginRequest { Email = "notanemail", Password = "pass" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Login_EmptyPassword_Fails()
    {
        var v = new LoginRequestValidator();
        var r = await v.ValidateAsync(new LoginRequest { Email = "test@test.com", Password = "" });
        r.IsValid.Should().BeFalse();
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Setup_ValidRequest_Passes()
    {
        var v = new SetupRequestValidator();
        var r = await v.ValidateAsync(new SetupRequest { Email = "a@b.com", Password = "12345678", FirstName = "F", LastName = "L" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Setup_ShortPassword_Fails()
    {
        var v = new SetupRequestValidator();
        var r = await v.ValidateAsync(new SetupRequest { Email = "a@b.com", Password = "short", FirstName = "F", LastName = "L" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Setup_EmptyFirstName_Fails()
    {
        var v = new SetupRequestValidator();
        var r = await v.ValidateAsync(new SetupRequest { Email = "a@b.com", Password = "12345678", FirstName = "", LastName = "L" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Setup_EmptyLastName_Fails()
    {
        var v = new SetupRequestValidator();
        var r = await v.ValidateAsync(new SetupRequest { Email = "a@b.com", Password = "12345678", FirstName = "F", LastName = "" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Setup_EmailTooLong_Fails()
    {
        var v = new SetupRequestValidator();
        var r = await v.ValidateAsync(new SetupRequest { Email = new string('a', 251) + "@b.com", Password = "12345678", FirstName = "F", LastName = "L" });
        r.IsValid.Should().BeFalse();
    }

    // ── CreateCompany ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCompany_ValidRequest_Passes()
    {
        var v = new CreateCompanyRequestValidator();
        var r = await v.ValidateAsync(new CreateCompanyRequest
        {
            CompanyName = "ACME", ManagerEmail = "m@a.com", ManagerPassword = "12345678",
            ManagerFirstName = "John", ManagerLastName = "Doe"
        });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCompany_MissingCompanyName_Fails()
    {
        var v = new CreateCompanyRequestValidator();
        var r = await v.ValidateAsync(new CreateCompanyRequest
        {
            CompanyName = "", ManagerEmail = "m@a.com", ManagerPassword = "12345678",
            ManagerFirstName = "John", ManagerLastName = "Doe"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCompany_ShortPassword_Fails()
    {
        var v = new CreateCompanyRequestValidator();
        var r = await v.ValidateAsync(new CreateCompanyRequest
        {
            CompanyName = "ACME", ManagerEmail = "m@a.com", ManagerPassword = "short",
            ManagerFirstName = "John", ManagerLastName = "Doe"
        });
        r.IsValid.Should().BeFalse();
    }

    // ── CreateTask ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_ValidRequest_Passes()
    {
        var v = new CreateTaskRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskRequest { Title = "T", Priority = "Medium", AssigneeIds = [Guid.NewGuid()] });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTask_EmptyTitle_Fails()
    {
        var v = new CreateTaskRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskRequest { Title = "", Priority = "Medium", AssigneeIds = [Guid.NewGuid()] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTask_TitleTooLong_Fails()
    {
        var v = new CreateTaskRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskRequest { Title = new string('A', 301), Priority = "Medium", AssigneeIds = [Guid.NewGuid()] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTask_InvalidPriority_Fails()
    {
        var v = new CreateTaskRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskRequest { Title = "T", Priority = "SuperUrgent", AssigneeIds = [Guid.NewGuid()] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTask_EmptyAssignees_Fails()
    {
        var v = new CreateTaskRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskRequest { Title = "T", Priority = "Medium", AssigneeIds = [] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTask_DescriptionTooLong_Fails()
    {
        var v = new CreateTaskRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskRequest { Title = "T", Priority = "Medium", AssigneeIds = [Guid.NewGuid()], Description = new string('X', 4001) });
        r.IsValid.Should().BeFalse();
    }

    // ── CreateTaskFromTemplate ────────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskFromTemplate_ValidRequest_Passes()
    {
        var v = new CreateTaskFromTemplateRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskFromTemplateRequest { Priority = "Medium", AssigneeIds = [Guid.NewGuid()] });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTaskFromTemplate_EmptyAssignees_Fails()
    {
        var v = new CreateTaskFromTemplateRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskFromTemplateRequest { Priority = "Medium", AssigneeIds = [] });
        r.IsValid.Should().BeFalse();
    }

    // ── UpdateTask ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTask_ValidStatus_Passes()
    {
        var v = new UpdateTaskRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskRequest { Status = "InProgress" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTask_InvalidStatus_Fails()
    {
        var v = new UpdateTaskRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskRequest { Status = "Cancelled" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTask_InvalidPriority_Fails()
    {
        var v = new UpdateTaskRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskRequest { Priority = "ASAP" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTask_EmptyTitle_Fails()
    {
        var v = new UpdateTaskRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskRequest { Title = "" });
        r.IsValid.Should().BeFalse();
    }

    // ── Task Steps ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskStep_ValidRequest_Passes()
    {
        var v = new CreateTaskStepRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskStepRequest { Title = "Step 1" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTaskStep_EmptyTitle_Fails()
    {
        var v = new CreateTaskStepRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskStepRequest { Title = "" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTaskStep_InstructionsTooLong_Fails()
    {
        var v = new CreateTaskStepRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskStepRequest { Title = "S", Instructions = new string('X', 4001) });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTaskStep_ValidRequest_Passes()
    {
        var v = new UpdateTaskStepRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskStepRequest { Title = "Updated" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTaskStep_EmptyTitle_Fails()
    {
        var v = new UpdateTaskStepRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskStepRequest { Title = "" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReorderTaskSteps_DuplicateIds_Fails()
    {
        var v = new ReorderTaskStepsRequestValidator();
        var id = Guid.NewGuid();
        var r = await v.ValidateAsync(new ReorderTaskStepsRequest { StepIds = [id, id] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReorderTaskSteps_Empty_Fails()
    {
        var v = new ReorderTaskStepsRequestValidator();
        var r = await v.ValidateAsync(new ReorderTaskStepsRequest { StepIds = [] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReorderTaskSteps_ValidRequest_Passes()
    {
        var v = new ReorderTaskStepsRequestValidator();
        var r = await v.ValidateAsync(new ReorderTaskStepsRequest { StepIds = [Guid.NewGuid(), Guid.NewGuid()] });
        r.IsValid.Should().BeTrue();
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_ValidRequest_Passes()
    {
        var v = new CreateTaskCommentRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskCommentRequest { Content = "<p>Hello</p>" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateComment_EmptyContent_Fails()
    {
        var v = new CreateTaskCommentRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskCommentRequest { Content = "" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateComment_TooLong_Fails()
    {
        var v = new CreateTaskCommentRequestValidator();
        var r = await v.ValidateAsync(new CreateTaskCommentRequest { Content = new string('X', 10001) });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateComment_EmptyContent_Fails()
    {
        var v = new UpdateTaskCommentRequestValidator();
        var r = await v.ValidateAsync(new UpdateTaskCommentRequest { Content = "" });
        r.IsValid.Should().BeFalse();
    }

    // ── Templates ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplate_ValidRequest_Passes()
    {
        var v = new CreateTemplateRequestValidator();
        var r = await v.ValidateAsync(new CreateTemplateRequest { Name = "T", Steps = [new() { Title = "S1" }] });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTemplate_EmptyName_Fails()
    {
        var v = new CreateTemplateRequestValidator();
        var r = await v.ValidateAsync(new CreateTemplateRequest { Name = "", Steps = [new() { Title = "S1" }] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTemplate_NoSteps_Fails()
    {
        var v = new CreateTemplateRequestValidator();
        var r = await v.ValidateAsync(new CreateTemplateRequest { Name = "T", Steps = [] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTemplate_StepWithEmptyTitle_Fails()
    {
        var v = new CreateTemplateRequestValidator();
        var r = await v.ValidateAsync(new CreateTemplateRequest { Name = "T", Steps = [new() { Title = "" }] });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTemplate_EmptyName_Fails()
    {
        var v = new UpdateTemplateRequestValidator();
        var r = await v.ValidateAsync(new UpdateTemplateRequest { Name = "" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTemplate_NullName_Passes()
    {
        var v = new UpdateTemplateRequestValidator();
        var r = await v.ValidateAsync(new UpdateTemplateRequest { Name = null, Description = "Updated" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderTemplateSteps_DuplicateIds_Fails()
    {
        var v = new ReorderStepsRequestValidator();
        var id = Guid.NewGuid();
        var r = await v.ValidateAsync(new ReorderStepsRequest { StepIds = [id, id] });
        r.IsValid.Should().BeFalse();
    }

    // ── Users ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_ValidRequest_Passes()
    {
        var v = new CreateUserRequestValidator();
        var r = await v.ValidateAsync(new CreateUserRequest
        {
            Email = "u@t.com", Password = "12345678",
            FirstName = "F", LastName = "L", Role = "User"
        });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_InvalidRole_Fails()
    {
        var v = new CreateUserRequestValidator();
        var r = await v.ValidateAsync(new CreateUserRequest
        {
            Email = "u@t.com", Password = "12345678",
            FirstName = "F", LastName = "L", Role = "Admin"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUser_ShortPassword_Fails()
    {
        var v = new CreateUserRequestValidator();
        var r = await v.ValidateAsync(new CreateUserRequest
        {
            Email = "u@t.com", Password = "short",
            FirstName = "F", LastName = "L", Role = "User"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUser_EmptyEmail_Fails()
    {
        var v = new CreateUserRequestValidator();
        var r = await v.ValidateAsync(new CreateUserRequest
        {
            Email = "", Password = "12345678",
            FirstName = "F", LastName = "L", Role = "User"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUser_ValidRole_Passes()
    {
        var v = new UpdateUserRequestValidator();
        var r = await v.ValidateAsync(new UpdateUserRequest { Role = "Manager" });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_InvalidRole_Fails()
    {
        var v = new UpdateUserRequestValidator();
        var r = await v.ValidateAsync(new UpdateUserRequest { Role = "SuperAdmin" });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUser_FirstNameTooLong_Fails()
    {
        var v = new UpdateUserRequestValidator();
        var r = await v.ValidateAsync(new UpdateUserRequest { FirstName = new string('A', 101) });
        r.IsValid.Should().BeFalse();
    }

    // ── RegisterRequest ──────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Passes()
    {
        var v = new RegisterRequestValidator();
        var r = await v.ValidateAsync(new RegisterRequest
        {
            CompanyName = "My Company",
            Email = "user@example.com",
            Password = "StrongPass1!",
            FirstName = "John",
            LastName = "Doe"
        });
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Register_EmptyCompanyName_Fails()
    {
        var v = new RegisterRequestValidator();
        var r = await v.ValidateAsync(new RegisterRequest
        {
            CompanyName = "",
            Email = "user@example.com",
            Password = "StrongPass1!",
            FirstName = "John",
            LastName = "Doe"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Register_EmptyEmail_Fails()
    {
        var v = new RegisterRequestValidator();
        var r = await v.ValidateAsync(new RegisterRequest
        {
            CompanyName = "My Company",
            Email = "",
            Password = "StrongPass1!",
            FirstName = "John",
            LastName = "Doe"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Register_ShortPassword_Fails()
    {
        var v = new RegisterRequestValidator();
        var r = await v.ValidateAsync(new RegisterRequest
        {
            CompanyName = "My Company",
            Email = "user@example.com",
            Password = "short",
            FirstName = "John",
            LastName = "Doe"
        });
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Register_EmptyFirstName_Fails()
    {
        var v = new RegisterRequestValidator();
        var r = await v.ValidateAsync(new RegisterRequest
        {
            CompanyName = "My Company",
            Email = "user@example.com",
            Password = "StrongPass1!",
            FirstName = "",
            LastName = "Doe"
        });
        r.IsValid.Should().BeFalse();
    }
}
