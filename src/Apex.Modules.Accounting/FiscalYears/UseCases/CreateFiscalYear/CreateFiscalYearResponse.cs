namespace Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;

public sealed record CreateFiscalYearResponse(
    long Id, long AccountingBookId, string Title, DateOnly StartDate, DateOnly EndDate,
    string Status, DateOnly FinalizedThroughDate, long NextDocumentNumber);
