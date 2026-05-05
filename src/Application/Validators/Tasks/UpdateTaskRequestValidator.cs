using Application.DTOs.Tasks;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators.Tasks;

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    private static readonly string[] ValidStatuses = Enum.GetNames<TaskItemStatus>();
    private static readonly string[] ValidPriorities = Enum.GetNames<TaskPriority>();

    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Task title cannot be empty.")
            .MaximumLength(300)
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<TaskItemStatus>())}.")
            .When(x => x.Status is not null);

        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<TaskPriority>())}.")
            .When(x => x.Priority is not null);

        RuleFor(x => x.AssigneeIds)
            .NotEmpty().WithMessage("At least one assignee is required.")
            .ForEach(id => id.NotEmpty().WithMessage("Assignee ID cannot be empty."))
            .When(x => x.AssigneeIds is not null);
    }
}
