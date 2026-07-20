namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetAccountBalances;

public sealed class GetAccountBalancesRequest
{
    public long AccountingBookId { get; init; }
    public long FiscalYearId { get; init; }
    public DateOnly AsOfDate { get; init; }
}
