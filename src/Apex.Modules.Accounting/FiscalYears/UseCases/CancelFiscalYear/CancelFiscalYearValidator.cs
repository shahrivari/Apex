using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;

public sealed class CancelFiscalYearValidator : AbstractValidator<CancelFiscalYearRequest>
{
    public CancelFiscalYearValidator() => RuleFor(x => x.CancellationDate).NotEqual(DateOnly.MinValue);
}
