namespace Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;

public sealed record FiscalYearItem(
    long Id, long AccountingBookId, string Title, DateOnly StartDate, DateOnly EndDate,
    string Status, DateOnly FinalizedThroughDate, long NextDocumentNumber,
    DateOnly? CancellationDate);

public sealed record ListFiscalYearsResponse(
    IReadOnlyList<FiscalYearItem> Items, int TotalCount, int Page, int PageSize);
