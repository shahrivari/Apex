using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public sealed class FinalizeFiscalYearHandler(
    IFiscalYearWriteRepository writeRepository, IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory, FiscalYearDirectorySynchronizer directorySynchronizer,
    IJournalEntryFinalizationRepository finalizationRepository,
    IClock clock, IValidator<FinalizeFiscalYearRequest> validator)
{
    public async Task<FinalizeFiscalYearResponse> HandleAsync(long id, FinalizeFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(id), beginTransaction: true, cancellationToken);
        var fiscalYear = await writeRepository.GetByIdForUpdateAsync(shard, id, cancellationToken)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
        if (fiscalYear.Status != FiscalYearStatus.Open)
            throw new BusinessRuleException(
                "Only an open fiscal year can be finalized.",
                FiscalYearErrors.CannotBeFinalized);
        var previousBoundary = fiscalYear.FinalizedThroughDate;
        if (request.FinalizedThroughDate == previousBoundary)
        {
            await shard.Transaction!.CommitAsync(cancellationToken);
            return new FinalizeFiscalYearResponse(
                fiscalYear.Id, fiscalYear.FinalizedThroughDate, fiscalYear.UpdatedAt);
        }
        if (previousBoundary == DateOnly.MaxValue
            || request.FinalizedThroughDate != previousBoundary.AddDays(1))
            throw new ConflictException(
                "Daily finalization must advance exactly one day.",
                JournalEntryErrors.InvalidFinalizationDate);
        if (await finalizationRepository.HasBlockingDraftsAsync(
                shard, id, previousBoundary, request.FinalizedThroughDate, cancellationToken))
            throw new ConflictException(
                "Draft journal entries block daily finalization.",
                JournalEntryErrors.DraftsBlockFinalization);
        if (!await finalizationRepository.AreProjectionsReconciledAsync(
                shard, fiscalYear.AccountingBookId, id,
                request.FinalizedThroughDate, cancellationToken))
            throw new ConflictException(
                "Financial projections do not reconcile with posted journal entries.",
                JournalEntryErrors.ProjectionReconciliationFailed);

        await finalizationRepository.RenumberAndFinalizeAsync(
            shard, id, request.FinalizedThroughDate,
            fiscalYear.NextJournalEntryNumber, cancellationToken);
        fiscalYear.FinalizeNextDay(request.FinalizedThroughDate, clock.UtcNow);
        await writeRepository.UpdateAsync(shard, fiscalYear, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        await directorySynchronizer.UpsertBestEffortAsync(fiscalYear, cancellationToken);
        return new FinalizeFiscalYearResponse(
            fiscalYear.Id, fiscalYear.FinalizedThroughDate, fiscalYear.UpdatedAt);
    }
}
