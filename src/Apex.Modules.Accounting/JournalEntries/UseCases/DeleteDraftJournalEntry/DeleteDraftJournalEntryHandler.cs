using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.DeleteDraftJournalEntry;

public sealed class DeleteDraftJournalEntryHandler(
    JournalEntryActivityValidator activityValidator,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IJournalEntryWriteRepository writeRepository)
{
    public async Task HandleAsync(long fiscalYearId, long id, CancellationToken cancellationToken = default)
    {
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);

        var fiscalYear = await activityValidator.LockAsync(shard, fiscalYearId, cancellationToken);
        var entry = await writeRepository.GetForUpdateAsync(shard, fiscalYearId, id, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);
        entry.EnsureDraft();

        // The accounting date must remain open and later than the finalized-through date.
        await activityValidator.ValidateAsync(
            fiscalYear, entry.AccountingBookId, entry.AccountingDate, cancellationToken);

        await writeRepository.DeleteAsync(shard, fiscalYearId, id, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
    }
}
