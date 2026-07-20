namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntryAudit;

public sealed class GetJournalEntryAuditRequest
{
    public long AccountingBookId { get; init; }
    public long FiscalYearId { get; init; }
    public long ReferenceNumber { get; init; }
}
