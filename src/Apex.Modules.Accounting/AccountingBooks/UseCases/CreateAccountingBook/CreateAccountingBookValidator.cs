using Apex.Modules.Accounting.AccountingBooks.Domain;
using FluentValidation;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

public sealed class CreateAccountingBookValidator : AbstractValidator<CreateAccountingBookRequest>
{
    public CreateAccountingBookValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(64).WithMessage("Code must not exceed 64 characters.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(256).WithMessage("Title must not exceed 256 characters.");

        RuleFor(x => x.OwnerType)
            .NotEmpty().WithMessage("Owner type is required.")
            .MaximumLength(64).WithMessage("Owner type must not exceed 64 characters.");

        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("Owner ID is required.")
            .MaximumLength(128).WithMessage("Owner ID must not exceed 128 characters.");
    }
}
