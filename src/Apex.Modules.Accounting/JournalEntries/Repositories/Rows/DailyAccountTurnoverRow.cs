namespace Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

public sealed class DailyAccountTurnoverRow
{
    public decimal DebitTurnover { get; init; }
    public decimal CreditTurnover { get; init; }
    public decimal NetTurnover { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int ProjectionVersion { get; init; }
}
