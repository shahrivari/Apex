namespace Apex.Modules.Accounting.FiscalYears.Repositories.Rows;

public sealed class FiscalYearRow
{
    public long Id { get; init; }
    public long AccountingBookId { get; init; }
    public string Title { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = null!;
    public DateOnly FinalizedThroughDate { get; init; }
    public long NextDocumentNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public DateOnly? CancellationDate { get; init; }
}
