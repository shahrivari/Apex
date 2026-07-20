using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;

public sealed class CreateDraftJournalEntryHandler(
    IValidator<CreateDraftJournalEntryRequest> validator,
    JournalEntryActivityValidator activityValidator,
    IFiscalYearWriteRepository fiscalYearRepository,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IJournalEntryWriteRepository writeRepository,
    JournalEntryLineAssembler lineAssembler,
    IIdGenerator idGenerator,
    IClock clock)
{
    public async Task<JournalEntryDetailResponse> HandleAsync(
        CreateDraftJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var documentType = JournalEntryHeaderTypes.ParseDocumentType(request.DocumentType);
        var insertionType = JournalEntryHeaderTypes.ParseInsertionType(request.InsertionType);
        var balanceEffect = JournalEntryHeaderTypes.ParseBalanceEffect(request.BalanceEffect);
        var sourceType = Normalize(request.SourceType);
        var sourceReference = Normalize(request.SourceReference);

        var shardKey = shardKeyFactory.Create(request.FiscalYearId);

        var lines = await lineAssembler.BuildAsync(request.Lines, cancellationToken);

        var now = clock.UtcNow;
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKey, beginTransaction: true, cancellationToken);
        var fiscalYear = await activityValidator.ValidateAsync(
            shard, request.FiscalYearId, request.AccountingBookId, request.AccountingDate, cancellationToken);

        if (sourceReference is not null)
        {
            var existing = await writeRepository.GetBySourceReferenceForUpdateAsync(
                shard, fiscalYear.Id, sourceType!, sourceReference, cancellationToken);
            if (existing is not null)
            {
                if (!IsEquivalent(
                        existing, request, fiscalYear.Id, documentType, insertionType, balanceEffect))
                    throw new ConflictException(
                        "A different journal entry already exists for the source reference.",
                        JournalEntryErrors.ConflictingIdempotentRequest);

                await shard.Transaction!.CommitAsync(cancellationToken);
                return JournalEntryDetailResponse.From(existing);
            }
        }

        var (referenceNumber, journalEntryNumber) = fiscalYear.AllocateJournalEntryNumbers();
        await fiscalYearRepository.UpdateAsync(shard, fiscalYear, cancellationToken);

        var entry = JournalEntry.Create(
            idGenerator.NewId(), request.AccountingBookId, fiscalYear.Id, referenceNumber, journalEntryNumber,
            request.AccountingDate, now, request.Description, documentType, insertionType, balanceEffect,
            sourceType, sourceReference, lines, now);

        await writeRepository.InsertAsync(shard, entry, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        return JournalEntryDetailResponse.From(entry);
    }

    private static bool IsEquivalent(
        JournalEntry existing,
        CreateDraftJournalEntryRequest request,
        long fiscalYearId,
        DocumentType documentType,
        InsertionType insertionType,
        BalanceEffect balanceEffect)
    {
        if (existing.AccountingBookId != request.AccountingBookId
            || existing.FiscalYearId != fiscalYearId
            || existing.AccountingDate != request.AccountingDate
            || !string.Equals(existing.Description, request.Description.Trim(), StringComparison.Ordinal)
            || existing.DocumentType != documentType
            || existing.InsertionType != insertionType
            || existing.BalanceEffect != balanceEffect
            || !string.Equals(existing.SourceType, Normalize(request.SourceType), StringComparison.Ordinal)
            || !string.Equals(existing.SourceReference, Normalize(request.SourceReference), StringComparison.Ordinal)
            || existing.Lines.Count != request.Lines.Count)
            return false;

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var actual = existing.Lines[index];
            var expected = request.Lines[index];
            if (!JournalEntrySideExtensions.TryParse(expected.Side, out var side)
                || actual.RowNumber != index + 1
                || actual.Side != side
                || actual.Amount != expected.Amount
                || !string.Equals(actual.AccountClassCode, expected.AccountClassCode.Trim(), StringComparison.Ordinal)
                || !string.Equals(actual.GeneralAccountCode, expected.GeneralAccountCode.Trim(), StringComparison.Ordinal)
                || !string.Equals(actual.SubsidiaryAccountCode, expected.SubsidiaryAccountCode.Trim(), StringComparison.Ordinal)
                || !string.Equals(actual.DetailAccountCode, Normalize(expected.DetailAccountCode), StringComparison.Ordinal)
                || !string.Equals(actual.Description, expected.Description.Trim(), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
