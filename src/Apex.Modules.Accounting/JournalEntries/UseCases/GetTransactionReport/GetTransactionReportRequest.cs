namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;

public sealed class GetTransactionReportRequest
{
    public long AccountingBookId { get; init; }
    public long FiscalYearId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? AccountClassCode { get; init; }
    public string? GeneralAccountCode { get; init; }
    public string? SubsidiaryAccountCode { get; init; }
    public string? DetailAccountCode { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}
