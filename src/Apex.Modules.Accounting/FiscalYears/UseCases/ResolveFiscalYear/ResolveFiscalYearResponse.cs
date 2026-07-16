namespace Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;

public sealed record ResolveFiscalYearResponse(
    long Id, long AccountingBookId, string Title, DateOnly StartDate, DateOnly EffectiveEndDate,
    string Status, DateOnly FinalizedThroughDate);
