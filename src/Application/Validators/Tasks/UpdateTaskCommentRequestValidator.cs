using Application.DTOs.Tasks;
using FluentValidation;

namespace Application.Validators.Tasks;

public class UpdateTaskCommentRequestValidator : AbstractValidator<UpdateTaskCommentRequest>
{
    public UpdateTaskCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Comment content is required.")
            .MaximumLength(10000).WithMessage("Comment content cannot exceed 10,000 characters.");
    }
}
