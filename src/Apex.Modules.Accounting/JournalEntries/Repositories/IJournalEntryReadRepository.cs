using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public interface IJournalEntryReadRepository
{
    Task<JournalEntryWithLines?> GetByIdAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default);

    Task<JournalEntryWithLines?> GetByReferenceNumberAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber,
        CancellationToken cancellationToken = default);

    Task<JournalEntryWithLines?> GetByJournalEntryNumberAsync(
        long accountingBookId, long fiscalYearId, long journalEntryNumber,
        CancellationToken cancellationToken = default);

    Task<JournalEntryWithLines?> GetBySourceReferenceAsync(
        long fiscalYearId, string? sourceType, string sourceReference,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<JournalEntryRow> Items, int TotalCount)> SearchAsync(
        JournalEntrySearchFilter filter, CancellationToken cancellationToken = default);
}
