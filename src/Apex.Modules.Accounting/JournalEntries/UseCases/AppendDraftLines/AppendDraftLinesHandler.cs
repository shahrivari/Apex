using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;

public sealed class AppendDraftLinesHandler(
    IValidator<AppendDraftLinesRequest> validator,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IJournalEntryWriteRepository writeRepository,
    JournalEntryActivityValidator activityValidator,
    JournalEntryLineAssembler lineAssembler,
    IClock clock)
{
    public async Task<JournalEntryDetailResponse> HandleAsync(
        long fiscalYearId, long id, AppendDraftLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var inputs = await lineAssembler.BuildAsync(request.Lines, cancellationToken);
        var now = clock.UtcNow;

        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);

        var fiscalYear = await activityValidator.LockAsync(shard, fiscalYearId, cancellationToken);
        var entry = await writeRepository.GetForUpdateAsync(shard, fiscalYearId, id, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);

        await activityValidator.ValidateAsync(
            fiscalYear, entry.AccountingBookId, entry.AccountingDate, cancellationToken);
        entry.AppendLines(inputs, now);
        await writeRepository.ReplaceLinesAsync(shard, fiscalYearId, entry, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        return JournalEntryDetailResponse.From(entry);
    }
}
