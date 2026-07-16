using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

public sealed class UpdateFiscalYearValidator : AbstractValidator<UpdateFiscalYearRequest>
{
    public UpdateFiscalYearValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.StartDate).NotEqual(DateOnly.MinValue);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}
