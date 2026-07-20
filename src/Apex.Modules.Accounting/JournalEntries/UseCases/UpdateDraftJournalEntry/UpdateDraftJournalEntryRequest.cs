namespace Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;

public sealed class UpdateDraftJournalEntryRequest
{
    public DateOnly AccountingDate { get; init; }
    public string Description { get; init; } = null!;
    public string DocumentType { get; init; } = null!;
    public string BalanceEffect { get; init; } = null!;
}
