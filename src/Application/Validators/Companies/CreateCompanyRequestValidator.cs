using Application.DTOs.Companies;
using FluentValidation;

namespace Application.Validators.Companies;

public class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200);

        RuleFor(x => x.ManagerEmail)
            .NotEmpty().WithMessage("Manager email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);

        RuleFor(x => x.ManagerPassword)
            .NotEmpty().WithMessage("Manager password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.ManagerFirstName)
            .NotEmpty().WithMessage("Manager first name is required.")
            .MaximumLength(100);

        RuleFor(x => x.ManagerLastName)
            .NotEmpty().WithMessage("Manager last name is required.")
            .MaximumLength(100);
    }
}
