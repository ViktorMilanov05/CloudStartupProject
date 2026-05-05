using Application.DTOs.Tasks;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators.Tasks;

public class CreateTaskFromTemplateRequestValidator : AbstractValidator<CreateTaskFromTemplateRequest>
{
    private static readonly string[] ValidPriorities = Enum.GetNames<TaskPriority>();

    public CreateTaskFromTemplateRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(300)
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.AssigneeIds)
            .NotEmpty().WithMessage("At least one assignee is required.")
            .ForEach(id => id.NotEmpty().WithMessage("Assignee ID cannot be empty."));

        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<TaskPriority>())}.");

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .WithMessage("Due date must be in the future.")
            .When(x => x.DueDate.HasValue);
    }
}
