using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ResolveAccountPath;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ValidateDetailAccountForPosting;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.PostJournalEntry;

public sealed class PostJournalEntryHandler(
    JournalEntryActivityValidator activityValidator,
    IAccountPathResolver accountPathResolver,
    IDetailAccountPostingValidator detailAccountPostingValidator,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory,
    IJournalEntryWriteRepository writeRepository,
    IJournalEntryProjectionWriteRepository projectionWriteRepository,
    IClock clock)
{
    public async Task<JournalEntryDetailResponse> HandleAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(fiscalYearId), beginTransaction: true, cancellationToken);

        var fiscalYear = await activityValidator.LockAsync(shard, fiscalYearId, cancellationToken);
        var entry = await writeRepository.GetForUpdateAsync(shard, fiscalYearId, id, cancellationToken)
            ?? throw new NotFoundException("Journal entry was not found.", JournalEntryErrors.NotFound);
        entry.EnsureDraft();

        // Posting revalidates all current rules, not only those checked when the draft was created.
        await activityValidator.ValidateAsync(
            fiscalYear, entry.AccountingBookId, entry.AccountingDate, cancellationToken);
        await ValidateLinesAsync(entry, cancellationToken);

        entry.Post(now);

        await writeRepository.MarkPostedAsync(shard, fiscalYearId, entry, cancellationToken);
        if (entry.BalanceEffect == BalanceEffect.Financial)
            await projectionWriteRepository.ApplyPostingAsync(shard, entry, now, cancellationToken);

        await shard.Transaction!.CommitAsync(cancellationToken);
        return JournalEntryDetailResponse.From(entry);
    }

    private async Task ValidateLinesAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        foreach (var line in entry.Lines)
        {
            var resolution = await accountPathResolver.ResolveAsync(
                line.AccountClassCode, line.GeneralAccountCode, line.SubsidiaryAccountCode, cancellationToken);
            if (!resolution.Exists)
                throw new BusinessRuleException(
                    "The account-code path does not exist.", JournalEntryErrors.InvalidAccountCodePath);
            if (!resolution.PostingEligible)
                throw new BusinessRuleException(
                    "The account is not eligible for posting.", JournalEntryErrors.AccountNotEligible);
            await detailAccountPostingValidator.ValidateAsync(
                line.DetailAccountCode, resolution.RequiredDetailType, cancellationToken);
        }
    }
}
