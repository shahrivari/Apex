namespace Apex.Modules.Accounting.FiscalYears.Domain;

public enum FiscalYearStatus
{
    Draft,
    Open,
    Closed,
    Cancelled
}

public static class FiscalYearStatusExtensions
{
    public static string ToDatabaseValue(this FiscalYearStatus status) => status switch
    {
        FiscalYearStatus.Draft => "DRAFT",
        FiscalYearStatus.Open => "OPEN",
        FiscalYearStatus.Closed => "CLOSED",
        FiscalYearStatus.Cancelled => "CANCELLED",
        _ => throw new InvalidOperationException($"Unknown fiscal year status: {status}.")
    };

    public static FiscalYearStatus FromDatabaseValue(string value) => value switch
    {
        "DRAFT" => FiscalYearStatus.Draft,
        "OPEN" => FiscalYearStatus.Open,
        "CLOSED" => FiscalYearStatus.Closed,
        "CANCELLED" => FiscalYearStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown fiscal year status: {value}.")
    };
}
