namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public sealed class FinalizeFiscalYearRequest
{
    public DateOnly FinalizedThroughDate { get; init; }
}
