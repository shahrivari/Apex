namespace Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;

public sealed record CancelFiscalYearResponse(long Id, string Status, DateOnly? CancellationDate, DateTime? CancelledAt);
