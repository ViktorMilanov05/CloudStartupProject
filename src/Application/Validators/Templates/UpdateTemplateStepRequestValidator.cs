using Application.DTOs.Templates;
using FluentValidation;

namespace Application.Validators.Templates;

public class UpdateTemplateStepRequestValidator : AbstractValidator<UpdateTemplateStepRequest>
{
    public UpdateTemplateStepRequestValidator()
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
