using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntryAudit;

public sealed record JournalEntryAuditItem(
    long Id,
    long ReferenceNumber,
    long JournalEntryNumber,
    DateOnly AccountingDate,
    string Status,
    long? ReversalOfReferenceNumber,
    long? ReversedByReferenceNumber,
    string? ReversalReason,
    DateTime? PostedAt)
{
    internal static JournalEntryAuditItem From(JournalEntryRow row) => new(
        row.Id, row.ReferenceNumber, row.JournalEntryNumber, row.AccountingDate,
        row.Status, row.ReversalOfReferenceNumber, row.ReversedByReferenceNumber,
        row.ReversalReason, row.PostedAt);
}
