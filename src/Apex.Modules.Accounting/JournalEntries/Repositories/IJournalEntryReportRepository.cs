using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public interface IJournalEntryReportRepository
{
    Task<IReadOnlyList<AccountReportRow>> GetTrialBalanceAsync(
        long accountingBookId, long fiscalYearId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<string> excludedDocumentTypes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountReportRow>> GetBalanceAsOfAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountReportRow>> GetTurnoverAsync(
        long accountingBookId, long fiscalYearId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<string> excludedDocumentTypes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JournalTransactionRow>> GetTransactionsAsync(
        long accountingBookId, long fiscalYearId, DateOnly? fromDate, DateOnly? toDate,
        string? accountClassCode, string? generalAccountCode, string? subsidiaryAccountCode,
        string? detailAccountCode, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JournalEntryRow>> GetAuditHistoryAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber,
        CancellationToken cancellationToken = default);
}
