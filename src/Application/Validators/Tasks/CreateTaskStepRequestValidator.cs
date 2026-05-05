using Application.DTOs.Tasks;
using FluentValidation;

namespace Application.Validators.Tasks;

public class CreateTaskStepRequestValidator : AbstractValidator<CreateTaskStepRequest>
{
    public CreateTaskStepRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Step title is required.")
            .MaximumLength(300);

        RuleFor(x => x.Instructions)
            .MaximumLength(4000)
            .When(x => x.Instructions is not null);
    }
}
