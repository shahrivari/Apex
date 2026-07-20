using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReconcileJournalEntryProjections;

public sealed record ReconcileJournalEntryProjectionsResponse(
    bool IsReconciled,
    IReadOnlyList<ProjectionMismatchRow> Mismatches);
