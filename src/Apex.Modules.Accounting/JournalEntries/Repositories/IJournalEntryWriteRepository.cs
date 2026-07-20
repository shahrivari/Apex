using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

/// <summary>
/// Command-side operations for Journal Entries. All methods run against a shard connection that
/// the command handler opens and owns (including its transaction); the repository never opens,
/// commits, or rolls back the transaction.
/// </summary>
public interface IJournalEntryWriteRepository
{
    Task InsertAsync(
        IShardConnection connection, JournalEntry entry, CancellationToken cancellationToken = default);

    Task<JournalEntry?> GetForUpdateAsync(
        IShardConnection connection, long fiscalYearId, long id,
        CancellationToken cancellationToken = default);

    Task<JournalEntry?> GetBySourceReferenceForUpdateAsync(
        IShardConnection connection, long fiscalYearId, string sourceType, string sourceReference,
        CancellationToken cancellationToken = default);

    Task MarkPostedAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry entry,
        CancellationToken cancellationToken = default);

    Task UpdateHeaderAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry entry,
        CancellationToken cancellationToken = default);

    Task ReplaceLinesAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry entry,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IShardConnection connection, long fiscalYearId, long id,
        CancellationToken cancellationToken = default);
}
