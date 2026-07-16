namespace Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;

public sealed class CancelFiscalYearRequest
{
    public DateOnly CancellationDate { get; init; }
}
