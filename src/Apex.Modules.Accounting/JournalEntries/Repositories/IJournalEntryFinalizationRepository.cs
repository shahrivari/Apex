using Apex.Application.Abstractions.Data;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public interface IJournalEntryFinalizationRepository
{
    Task<bool> HasBlockingDraftsAsync(
        IShardConnection connection, long fiscalYearId, DateOnly afterDate, DateOnly throughDate,
        CancellationToken cancellationToken = default);

    Task<bool> AreProjectionsReconciledAsync(
        IShardConnection connection, long accountingBookId, long fiscalYearId, DateOnly throughDate,
        CancellationToken cancellationToken = default);

    Task RenumberAndFinalizeAsync(
        IShardConnection connection, long fiscalYearId, DateOnly throughDate,
        long temporaryNumberBase, CancellationToken cancellationToken = default);
}
