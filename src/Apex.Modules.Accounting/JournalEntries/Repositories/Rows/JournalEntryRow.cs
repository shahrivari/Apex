namespace Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

public sealed class JournalEntryRow
{
    public long Id { get; init; }
    public long AccountingBookId { get; init; }
    public long FiscalYearId { get; init; }
    public long ReferenceNumber { get; init; }
    public long JournalEntryNumber { get; init; }
    public bool NumberFinalized { get; init; }
    public DateOnly AccountingDate { get; init; }
    public DateTime RegisteredAt { get; init; }
    public string Description { get; init; } = null!;
    public string DocumentType { get; init; } = null!;
    public string InsertionType { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string BalanceEffect { get; init; } = null!;
    public string? SourceType { get; init; }
    public string? SourceReference { get; init; }
    public long? ReversalOfReferenceNumber { get; init; }
    public long? ReversedByReferenceNumber { get; init; }
    public string? ReversalReason { get; init; }
    public DateTime? PostedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
