using Application.DTOs.Templates;
using FluentValidation;

namespace Application.Validators.Templates;

public class UpdateTemplateRequestValidator : AbstractValidator<UpdateTemplateRequest>
{
    public UpdateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name cannot be empty.")
            .MaximumLength(300)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);
    }
}
