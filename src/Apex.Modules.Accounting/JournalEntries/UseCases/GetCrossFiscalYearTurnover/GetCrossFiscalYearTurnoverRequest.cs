namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetCrossFiscalYearTurnover;

public sealed class GetCrossFiscalYearTurnoverRequest
{
    public long AccountingBookId { get; init; }
    public IReadOnlyList<long> FiscalYearIds { get; init; } = [];
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public IReadOnlyList<string> ExcludedDocumentTypes { get; init; } = [];
}
