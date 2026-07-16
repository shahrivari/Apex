namespace Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;

public sealed record OpenFiscalYearResponse(long Id, string Status, DateTime? OpenedAt);
