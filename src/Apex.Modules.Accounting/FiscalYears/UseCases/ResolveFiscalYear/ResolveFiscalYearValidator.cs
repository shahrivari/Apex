using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;

public sealed class ResolveFiscalYearValidator : AbstractValidator<ResolveFiscalYearRequest>
{
    private static readonly string[] Statuses = ["DRAFT", "OPEN", "CLOSED", "CANCELLED"];

    public ResolveFiscalYearValidator()
    {
        RuleFor(x => x.AccountingBookId).GreaterThan(0);
        RuleFor(x => x.AccountingDate).NotEqual(DateOnly.MinValue);
        RuleFor(x => x.RequiredStatus)
            .Must(x => x is null || Statuses.Contains(x.Trim().ToUpperInvariant()))
            .WithMessage("Required status is invalid.");
    }
}
