using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

/// <summary>
/// Applies posting effects to the shard-resident financial projections (Daily Account Turnover and
/// Daily Account Balance). Runs on the posting command's shard connection and transaction so the
/// projection updates commit atomically with the entry's transition to posted.
/// </summary>
public interface IJournalEntryProjectionWriteRepository
{
    Task ApplyPostingAsync(
        IShardConnection connection, JournalEntry entry, DateTime updatedAt,
        CancellationToken cancellationToken = default);
}
