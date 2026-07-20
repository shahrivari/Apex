using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReconcileJournalEntryProjections;

public sealed class ReconcileJournalEntryProjectionsHandler(
    IShardConnectionFactory connectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IFiscalYearWriteRepository fiscalYearRepository,
    IJournalEntryProjectionMaintenanceRepository maintenanceRepository)
{
    public async Task<ReconcileJournalEntryProjectionsResponse> HandleAsync(
        long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var shard = await connectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);
        var fiscalYear = await fiscalYearRepository.GetByIdForUpdateAsync(
            shard, fiscalYearId, cancellationToken)
            ?? throw new NotFoundException("Fiscal year was not found.", JournalEntryErrors.FiscalYearNotFound);
        var mismatches = await maintenanceRepository.ReconcileAsync(
            shard, fiscalYear.AccountingBookId, fiscalYearId, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        return new ReconcileJournalEntryProjectionsResponse(mismatches.Count == 0, mismatches);
    }
}
