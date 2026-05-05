using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators.Users;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] ValidRoles = ["User", "Manager"];

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Role)
            .Must(r => ValidRoles.Contains(r))
            .WithMessage("Role must be 'User' or 'Manager'.");
    }
}
