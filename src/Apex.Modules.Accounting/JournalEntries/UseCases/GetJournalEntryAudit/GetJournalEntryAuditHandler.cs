using Apex.Modules.Accounting.JournalEntries.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntryAudit;

public sealed class GetJournalEntryAuditHandler(
    IValidator<GetJournalEntryAuditRequest> validator,
    IJournalEntryReportRepository repository)
{
    public async Task<IReadOnlyList<JournalEntryAuditItem>> HandleAsync(
        GetJournalEntryAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var rows = await repository.GetAuditHistoryAsync(
            request.AccountingBookId, request.FiscalYearId,
            request.ReferenceNumber, cancellationToken);
        return rows.Select(JournalEntryAuditItem.From).ToList();
    }
}
