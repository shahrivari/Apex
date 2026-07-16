using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public sealed class FinalizeFiscalYearValidator : AbstractValidator<FinalizeFiscalYearRequest>
{
    public FinalizeFiscalYearValidator() => RuleFor(x => x.FinalizedThroughDate).NotEqual(DateOnly.MinValue);
}
