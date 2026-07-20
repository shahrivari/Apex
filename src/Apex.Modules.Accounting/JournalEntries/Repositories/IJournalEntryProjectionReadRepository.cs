using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

/// <summary>
/// Reads the shard-resident financial projections. Closing balance is derived as the running sum
/// of the sparse per-date net movements through the requested date.
/// </summary>
public interface IJournalEntryProjectionReadRepository
{
    Task<DailyAccountTurnoverRow?> GetTurnoverAsync(
        long accountingBookId, long fiscalYearId, DateOnly balanceDate,
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        string? detailAccountCode, string documentType, CancellationToken cancellationToken = default);

    Task<decimal> GetClosingBalanceAsOfAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate,
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        string? detailAccountCode, CancellationToken cancellationToken = default);
}
