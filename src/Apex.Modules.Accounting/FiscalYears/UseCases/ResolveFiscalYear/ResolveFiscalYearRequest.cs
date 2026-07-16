namespace Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;

public sealed class ResolveFiscalYearRequest
{
    public long AccountingBookId { get; init; }
    public DateOnly AccountingDate { get; init; }
    public string? RequiredStatus { get; init; }
}
