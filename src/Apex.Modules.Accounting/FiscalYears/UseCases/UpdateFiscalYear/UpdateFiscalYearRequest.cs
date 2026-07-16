namespace Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

public sealed class UpdateFiscalYearRequest
{
    public string Title { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
}
