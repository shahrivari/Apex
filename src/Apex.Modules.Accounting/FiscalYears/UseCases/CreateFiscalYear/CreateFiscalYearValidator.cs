using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;

public sealed class CreateFiscalYearValidator : AbstractValidator<CreateFiscalYearRequest>
{
    public CreateFiscalYearValidator()
    {
        RuleFor(x => x.AccountingBookId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.StartDate).NotEqual(DateOnly.MinValue);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}
