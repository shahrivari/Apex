namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;

public sealed class ReverseJournalEntryRequest
{
    public DateOnly AccountingDate { get; init; }
    public string ReversalReason { get; init; } = null!;
}
