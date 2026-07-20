using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetCrossFiscalYearTurnover;

public sealed class GetCrossFiscalYearTurnoverHandler(
    IValidator<GetCrossFiscalYearTurnoverRequest> validator,
    IJournalEntryReportRepository repository)
{
    public async Task<IReadOnlyList<CrossFiscalYearTurnoverItem>> HandleAsync(
        GetCrossFiscalYearTurnoverRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var excluded = request.ExcludedDocumentTypes
            .Select(DocumentTypeExtensions.FromDatabaseValue)
            .Select(type => type.ToDatabaseValue()).Distinct().ToList();
        var reads = request.FiscalYearIds.Select(fiscalYearId => repository.GetTurnoverAsync(
            request.AccountingBookId, fiscalYearId, request.FromDate, request.ToDate,
            excluded, cancellationToken));
        var rows = (await Task.WhenAll(reads)).SelectMany(items => items);
        return rows
            .GroupBy(row => new
            {
                row.AccountClassCode,
                row.GeneralAccountCode,
                row.SubsidiaryAccountCode,
                row.DetailAccountCode
            })
            .Select(group => new CrossFiscalYearTurnoverItem(
                group.Key.AccountClassCode, group.Key.GeneralAccountCode,
                group.Key.SubsidiaryAccountCode,
                string.IsNullOrEmpty(group.Key.DetailAccountCode) ? null : group.Key.DetailAccountCode,
                group.Sum(row => row.DebitTurnover), group.Sum(row => row.CreditTurnover),
                group.Sum(row => row.DebitTurnover - row.CreditTurnover)))
            .OrderBy(item => item.AccountClassCode)
            .ThenBy(item => item.GeneralAccountCode)
            .ThenBy(item => item.SubsidiaryAccountCode)
            .ThenBy(item => item.DetailAccountCode)
            .ToList();
    }
}
