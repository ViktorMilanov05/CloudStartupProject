using Application.DTOs.Templates;
using FluentValidation;

namespace Application.Validators.Templates;

public class CreateTemplateStepRequestValidator : AbstractValidator<CreateTemplateStepRequest>
{
    public CreateTemplateStepRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Step title is required.")
            .MaximumLength(300);

        RuleFor(x => x.Instructions)
            .MaximumLength(4000)
            .When(x => x.Instructions is not null);
    }
}
