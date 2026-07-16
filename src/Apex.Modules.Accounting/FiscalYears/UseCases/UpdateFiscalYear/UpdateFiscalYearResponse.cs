namespace Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

public sealed record UpdateFiscalYearResponse(
    long Id, string Title, DateOnly StartDate, DateOnly EndDate, DateOnly FinalizedThroughDate, DateTime? UpdatedAt);
