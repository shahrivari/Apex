using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;

public sealed class ListFiscalYearsValidator : AbstractValidator<ListFiscalYearsRequest>
{
    private static readonly string[] Statuses = ["DRAFT", "OPEN", "CLOSED", "CANCELLED"];

    public ListFiscalYearsValidator()
    {
        RuleFor(x => x.AccountingBookId).GreaterThan(0).When(x => x.AccountingBookId.HasValue);
        RuleFor(x => x.Status).Must(x => x is null || Statuses.Contains(x.Trim().ToUpperInvariant()))
            .WithMessage("Status must be DRAFT, OPEN, CLOSED, or CANCELLED.");
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
