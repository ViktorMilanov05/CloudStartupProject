using Application.DTOs.Templates;
using FluentValidation;

namespace Application.Validators.Templates;

public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(300);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Steps)
            .NotEmpty().WithMessage("At least one step is required.");

        RuleForEach(x => x.Steps).SetValidator(new CreateTemplateStepRequestValidator());
    }
}
