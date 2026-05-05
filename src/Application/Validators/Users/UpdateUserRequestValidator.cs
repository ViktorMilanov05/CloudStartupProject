using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators.Users;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] ValidRoles = ["User", "Manager"];

    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null);

        RuleFor(x => x.Role)
            .Must(r => ValidRoles.Contains(r!))
            .WithMessage("Role must be 'User' or 'Manager'.")
            .When(x => x.Role is not null);
    }
}
