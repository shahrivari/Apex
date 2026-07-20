using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public interface IJournalEntryProjectionMaintenanceRepository
{
    Task RebuildAsync(
        IShardConnection connection, long accountingBookId, long fiscalYearId,
        DateOnly? fromDate, DateTime updatedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectionMismatchRow>> ReconcileAsync(
        IShardConnection connection, long accountingBookId, long fiscalYearId,
        CancellationToken cancellationToken = default);
}
