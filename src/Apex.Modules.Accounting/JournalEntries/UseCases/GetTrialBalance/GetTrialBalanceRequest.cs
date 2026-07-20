namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTrialBalance;

public sealed class GetTrialBalanceRequest
{
    public long AccountingBookId { get; init; }
    public long FiscalYearId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public IReadOnlyList<string> ExcludedDocumentTypes { get; init; } = [];
}
