namespace Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;

public sealed class CreateFiscalYearRequest
{
    public long AccountingBookId { get; init; }
    public string Title { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
}
