using Apex.Modules.Accounting.JournalEntries.Repositories;
using Apex.Modules.Accounting.JournalEntries.UseCases.Reporting;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetAccountBalances;

public sealed class GetAccountBalancesHandler(
    IValidator<GetAccountBalancesRequest> validator,
    IJournalEntryReportRepository repository)
{
    public async Task<IReadOnlyList<AccountReportItem>> HandleAsync(
        GetAccountBalancesRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var rows = await repository.GetBalanceAsOfAsync(
            request.AccountingBookId, request.FiscalYearId, request.AsOfDate, cancellationToken);
        return rows.Select(AccountReportItem.From).ToList();
    }
}
