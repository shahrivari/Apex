using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;

public sealed class UpdateDraftJournalEntryHandler(
    IValidator<UpdateDraftJournalEntryRequest> validator,
    JournalEntryActivityValidator activityValidator,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IJournalEntryWriteRepository writeRepository,
    IClock clock)
{
    public async Task<JournalEntryDetailResponse> HandleAsync(
        long fiscalYearId, long id, UpdateDraftJournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var documentType = JournalEntryHeaderTypes.ParseDocumentType(request.DocumentType);
        var balanceEffect = JournalEntryHeaderTypes.ParseBalanceEffect(request.BalanceEffect);
        var now = clock.UtcNow;

        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);

        var fiscalYear = await activityValidator.LockAsync(shard, fiscalYearId, cancellationToken);
        var entry = await writeRepository.GetForUpdateAsync(shard, fiscalYearId, id, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);

        await activityValidator.ValidateAsync(
            fiscalYear, entry.AccountingBookId, request.AccountingDate, cancellationToken);

        entry.UpdateHeader(request.AccountingDate, request.Description, documentType, balanceEffect, now);
        await writeRepository.UpdateHeaderAsync(shard, fiscalYearId, entry, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        return JournalEntryDetailResponse.From(entry);
    }
}
