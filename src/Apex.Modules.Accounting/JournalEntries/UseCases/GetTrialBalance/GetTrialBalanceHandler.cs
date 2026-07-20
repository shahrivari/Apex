using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories;
using Apex.Modules.Accounting.JournalEntries.UseCases.Reporting;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTrialBalance;

public sealed class GetTrialBalanceHandler(
    IValidator<GetTrialBalanceRequest> validator,
    IJournalEntryReportRepository repository)
{
    public async Task<IReadOnlyList<AccountReportItem>> HandleAsync(
        GetTrialBalanceRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var excluded = request.ExcludedDocumentTypes
            .Select(DocumentTypeExtensions.FromDatabaseValue)
            .Select(type => type.ToDatabaseValue()).Distinct().ToList();
        var rows = await repository.GetTrialBalanceAsync(
            request.AccountingBookId, request.FiscalYearId, request.FromDate,
            request.ToDate, excluded, cancellationToken);
        return rows.Select(AccountReportItem.From).ToList();
    }
}
