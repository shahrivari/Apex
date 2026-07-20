namespace Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

/// <summary>
/// A Journal Entry header together with its ordered lines, as read from a single shard query.
/// </summary>
public sealed record JournalEntryWithLines(
    JournalEntryRow Header,
    IReadOnlyList<JournalEntryLineRow> Lines);
