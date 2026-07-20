namespace Apex.Modules.Accounting.JournalEntries.UseCases;

/// <summary>
/// A single Journal Entry Line supplied by a client when creating or editing a draft. Shared by
/// the create, append, and replace use cases. Account codes are stored immutably on the line;
/// <see cref="RowNumber"/> is optional and defaults to automatic assignment.
/// </summary>
public sealed class JournalEntryLineRequest
{
    public string Side { get; init; } = null!;
    public decimal Amount { get; init; }
    public string AccountClassCode { get; init; } = null!;
    public string GeneralAccountCode { get; init; } = null!;
    public string SubsidiaryAccountCode { get; init; } = null!;
    public string? DetailAccountCode { get; init; }
    public string Description { get; init; } = null!;
    public int? RowNumber { get; init; }
}
