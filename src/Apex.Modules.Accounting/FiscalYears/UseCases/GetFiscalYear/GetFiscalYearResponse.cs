namespace Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;

public sealed record GetFiscalYearResponse(
    long Id, long AccountingBookId, string Title, DateOnly StartDate, DateOnly EndDate,
    string Status, DateOnly FinalizedThroughDate, long NextReferenceNumber,
    long NextJournalEntryNumber,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? OpenedAt, DateTime? ClosedAt,
    DateTime? CancelledAt, DateOnly? CancellationDate);
