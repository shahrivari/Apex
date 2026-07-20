namespace Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

public sealed class JournalEntryLineRow
{
    public long Id { get; init; }
    public long JournalEntryId { get; init; }
    public int RowNumber { get; init; }
    public string AccountClassCode { get; init; } = null!;
    public string GeneralAccountCode { get; init; } = null!;
    public string SubsidiaryAccountCode { get; init; } = null!;
    public string? DetailAccountCode { get; init; }
    public string Side { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Description { get; init; } = null!;
}
