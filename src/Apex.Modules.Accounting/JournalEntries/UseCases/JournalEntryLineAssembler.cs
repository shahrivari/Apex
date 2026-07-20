using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ResolveAccountPath;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ValidateDetailAccountForPosting;
using Apex.Modules.Accounting.JournalEntries.Domain;

namespace Apex.Modules.Accounting.JournalEntries.UseCases;

/// <summary>
/// Turns client line requests into validated domain <see cref="JournalEntryLineInput"/> values:
/// parses the side, allocates line identities, and — when the account-code path already exists —
/// verifies the detail-account requirement early. Full account-path existence and eligibility are
/// re-validated at posting (a later increment).
/// </summary>
public sealed class JournalEntryLineAssembler(
    IIdGenerator idGenerator,
    IAccountPathResolver accountPathResolver,
    IDetailAccountPostingValidator detailAccountPostingValidator)
{
    public async Task<IReadOnlyList<JournalEntryLineInput>> BuildAsync(
        IReadOnlyList<JournalEntryLineRequest> lines, CancellationToken cancellationToken)
    {
        var inputs = new List<JournalEntryLineInput>(lines.Count);
        foreach (var line in lines)
        {
            if (!JournalEntrySideExtensions.TryParse(line.Side, out var side))
                throw new BusinessRuleException(
                    "Unsupported journal entry side.", JournalEntryErrors.UnsupportedSide);

            var resolution = await accountPathResolver.ResolveAsync(
                line.AccountClassCode, line.GeneralAccountCode, line.SubsidiaryAccountCode, cancellationToken);
            if (resolution.Exists)
                await detailAccountPostingValidator.ValidateAsync(
                    line.DetailAccountCode, resolution.RequiredDetailType, cancellationToken);

            inputs.Add(new JournalEntryLineInput(
                idGenerator.NewId(), side, line.Amount, line.AccountClassCode, line.GeneralAccountCode,
                line.SubsidiaryAccountCode, line.DetailAccountCode, line.Description, line.RowNumber));
        }

        return inputs;
    }
}
