using FluentValidation;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public sealed class ListAccountingBooksValidator : AbstractValidator<ListAccountingBooksRequest>
{
    public ListAccountingBooksValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.")
            .When(x => x.Page is not null);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.")
            .When(x => x.PageSize is not null);
    }
}
