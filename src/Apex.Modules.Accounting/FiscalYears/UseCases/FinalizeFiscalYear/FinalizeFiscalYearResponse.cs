namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public sealed record FinalizeFiscalYearResponse(long Id, DateOnly FinalizedThroughDate, DateTime? UpdatedAt);
