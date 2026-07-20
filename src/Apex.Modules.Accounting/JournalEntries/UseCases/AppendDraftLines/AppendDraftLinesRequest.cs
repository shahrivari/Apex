namespace Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;

public sealed class AppendDraftLinesRequest
{
    public IReadOnlyList<JournalEntryLineRequest> Lines { get; init; } = [];
}
