using Apex.Modules.Accounting.JournalEntries.Repositories;
using Apex.Modules.Accounting.JournalEntries.UseCases.Reporting;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;

public sealed class GetTransactionReportHandler(
    IValidator<GetTransactionReportRequest> validator,
    IJournalEntryReportRepository repository)
{
    public async Task<IReadOnlyList<JournalTransactionItem>> HandleAsync(
        GetTransactionReportRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var rows = await repository.GetTransactionsAsync(
            request.AccountingBookId, request.FiscalYearId, request.FromDate, request.ToDate,
            Normalize(request.AccountClassCode), Normalize(request.GeneralAccountCode),
            Normalize(request.SubsidiaryAccountCode), Normalize(request.DetailAccountCode),
            request.Page, request.PageSize, cancellationToken);
        return rows.Select(JournalTransactionItem.From).ToList();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
