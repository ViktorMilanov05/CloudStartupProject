using Application.DTOs.Tasks;
using FluentValidation;

namespace Application.Validators.Tasks;

public class ReorderTaskStepsRequestValidator : AbstractValidator<ReorderTaskStepsRequest>
{
    public ReorderTaskStepsRequestValidator()
    {
        RuleFor(x => x.StepIds)
            .NotEmpty().WithMessage("Step IDs are required.");

        RuleFor(x => x.StepIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Step IDs must be unique.")
            .When(x => x.StepIds.Count > 0);
    }
}
