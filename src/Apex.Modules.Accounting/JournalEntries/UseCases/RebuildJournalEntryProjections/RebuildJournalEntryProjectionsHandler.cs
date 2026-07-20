using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.RebuildJournalEntryProjections;

public sealed class RebuildJournalEntryProjectionsHandler(
    IShardConnectionFactory connectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IFiscalYearWriteRepository fiscalYearRepository,
    IJournalEntryProjectionMaintenanceRepository maintenanceRepository,
    IClock clock)
{
    public async Task HandleAsync(
        long fiscalYearId, RebuildJournalEntryProjectionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var shard = await connectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);
        var fiscalYear = await fiscalYearRepository.GetByIdForUpdateAsync(
            shard, fiscalYearId, cancellationToken)
            ?? throw new NotFoundException("Fiscal year was not found.", JournalEntryErrors.FiscalYearNotFound);
        if (request.FromDate.HasValue
            && (request.FromDate <= fiscalYear.FinalizedThroughDate
                || request.FromDate < fiscalYear.StartDate
                || request.FromDate > fiscalYear.EffectiveEndDate))
            throw new ConflictException(
                "A partial rebuild must begin within the unfinalized Fiscal Year range.",
                JournalEntryErrors.ProjectionRebuildConflict);

        await maintenanceRepository.RebuildAsync(
            shard, fiscalYear.AccountingBookId, fiscalYearId,
            request.FromDate, clock.UtcNow, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
    }
}
