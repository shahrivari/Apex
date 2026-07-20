namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReplaceDraftLines;

public sealed class ReplaceDraftLinesRequest
{
    public IReadOnlyList<JournalEntryLineRequest> Lines { get; init; } = [];
}
