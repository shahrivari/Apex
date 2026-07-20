using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;

public sealed class ReverseJournalEntryHandler(
    IValidator<ReverseJournalEntryRequest> validator,
    JournalEntryActivityValidator activityValidator,
    IFiscalYearWriteRepository fiscalYearRepository,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IJournalEntryWriteRepository writeRepository,
    IJournalEntryProjectionWriteRepository projectionWriteRepository,
    IIdGenerator idGenerator,
    IClock clock)
{
    public async Task<JournalEntryDetailResponse> HandleAsync(
        long fiscalYearId, long originalReferenceNumber, ReverseJournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var now = clock.UtcNow;
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);

        var fiscalYear = await activityValidator.ValidateAsync(
            shard, fiscalYearId, request.AccountingDate, cancellationToken);
        var original = await writeRepository.GetByReferenceNumberForUpdateAsync(
            shard, fiscalYearId, originalReferenceNumber, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);

        var (referenceNumber, journalEntryNumber) = fiscalYear.AllocateJournalEntryNumbers();
        var reversalLineIds = original.Lines.Select(_ => idGenerator.NewId()).ToList();
        var reversal = JournalEntry.CreatePostedReversal(
            original, idGenerator.NewId(), referenceNumber, journalEntryNumber,
            request.AccountingDate, request.ReversalReason, reversalLineIds, now);

        await fiscalYearRepository.UpdateAsync(shard, fiscalYear, cancellationToken);
        await writeRepository.InsertAsync(shard, reversal, cancellationToken);
        await writeRepository.LinkReversalAsync(shard, fiscalYearId, original, cancellationToken);
        if (reversal.BalanceEffect == BalanceEffect.Financial)
            await projectionWriteRepository.ApplyPostingAsync(shard, reversal, now, cancellationToken);

        await shard.Transaction!.CommitAsync(cancellationToken);
        return JournalEntryDetailResponse.From(reversal);
    }
}
