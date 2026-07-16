namespace Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;

public sealed class ListFiscalYearsRequest
{
    public long? AccountingBookId { get; init; }
    public string? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
