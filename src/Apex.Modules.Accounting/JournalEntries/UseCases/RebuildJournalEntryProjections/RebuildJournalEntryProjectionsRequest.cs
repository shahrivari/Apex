namespace Apex.Modules.Accounting.JournalEntries.UseCases.RebuildJournalEntryProjections;

public sealed class RebuildJournalEntryProjectionsRequest
{
    public DateOnly? FromDate { get; init; }
}
