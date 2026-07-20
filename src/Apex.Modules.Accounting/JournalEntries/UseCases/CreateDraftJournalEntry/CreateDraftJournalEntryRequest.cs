namespace Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;

public sealed class CreateDraftJournalEntryRequest
{
    public long AccountingBookId { get; init; }
    public long FiscalYearId { get; init; }
    public DateOnly AccountingDate { get; init; }
    public string Description { get; init; } = null!;
    public string DocumentType { get; init; } = null!;
    public string InsertionType { get; init; } = null!;
    public string BalanceEffect { get; init; } = null!;
    public string? SourceType { get; init; }
    public string? SourceReference { get; init; }
    public IReadOnlyList<JournalEntryLineRequest> Lines { get; init; } = [];
}
