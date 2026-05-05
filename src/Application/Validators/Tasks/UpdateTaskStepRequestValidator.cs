using Application.DTOs.Tasks;
using FluentValidation;

namespace Application.Validators.Tasks;

public class UpdateTaskStepRequestValidator : AbstractValidator<UpdateTaskStepRequest>
{
    public UpdateTaskStepRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Step title cannot be empty.")
            .MaximumLength(300)
            .When(x => x.Title is not null);

        RuleFor(x => x.Instructions)
            .MaximumLength(4000)
            .When(x => x.Instructions is not null);
    }
}
