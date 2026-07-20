namespace Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;

public sealed record JournalEntrySummary(
    long Id,
    long AccountingBookId,
    long FiscalYearId,
    long ReferenceNumber,
    long JournalEntryNumber,
    bool NumberFinalized,
    DateOnly AccountingDate,
    string Description,
    string DocumentType,
    string InsertionType,
    string Status,
    string BalanceEffect);

public sealed record SearchJournalEntriesResponse(
    IReadOnlyList<JournalEntrySummary> Items, int TotalCount, int Page, int PageSize);
