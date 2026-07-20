using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryReportRepository(
    IShardConnectionFactory connectionFactory,
    IShardKeyFactory<long> shardKeyFactory) : IJournalEntryReportRepository
{
    public Task<IReadOnlyList<AccountReportRow>> GetTrialBalanceAsync(
        long accountingBookId, long fiscalYearId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<string> excludedDocumentTypes, CancellationToken cancellationToken = default) =>
        QueryAccountsAsync(TrialBalanceSql, accountingBookId, fiscalYearId, fromDate, toDate,
            excludedDocumentTypes, cancellationToken);

    public Task<IReadOnlyList<AccountReportRow>> GetBalanceAsOfAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate,
        CancellationToken cancellationToken = default) =>
        QueryAccountsAsync(BalanceAsOfSql, accountingBookId, fiscalYearId, null, asOfDate,
            [], cancellationToken);

    public Task<IReadOnlyList<AccountReportRow>> GetTurnoverAsync(
        long accountingBookId, long fiscalYearId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<string> excludedDocumentTypes, CancellationToken cancellationToken = default) =>
        QueryAccountsAsync(TurnoverSql, accountingBookId, fiscalYearId, fromDate, toDate,
            excludedDocumentTypes, cancellationToken);

    public async Task<IReadOnlyList<JournalTransactionRow>> GetTransactionsAsync(
        long accountingBookId, long fiscalYearId, DateOnly? fromDate, DateOnly? toDate,
        string? accountClassCode, string? generalAccountCode, string? subsidiaryAccountCode,
        string? detailAccountCode, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var shard = await OpenAsync(fiscalYearId, cancellationToken);
        var rows = await shard.Connection.QueryAsync<JournalTransactionRow>(new CommandDefinition(
            TransactionSql,
            new
            {
                AccountingBookId = accountingBookId,
                FiscalYearId = fiscalYearId,
                FromDate = fromDate,
                ToDate = toDate,
                AccountClassCode = accountClassCode,
                GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode,
                DetailAccountCode = detailAccountCode,
                Skip = (page - 1) * pageSize,
                PageSize = pageSize
            }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<JournalEntryRow>> GetAuditHistoryAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber,
        CancellationToken cancellationToken = default)
    {
        await using var shard = await OpenAsync(fiscalYearId, cancellationToken);
        var rows = await shard.Connection.QueryAsync<JournalEntryRow>(new CommandDefinition(
            $"""
            SELECT {JournalEntryReadRepository.HeaderColumns}
            FROM journal_entry
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND (reference_number = @ReferenceNumber
                OR reversal_of_reference_number = @ReferenceNumber
                OR reversed_by_reference_number = @ReferenceNumber)
            ORDER BY accounting_date, registered_at, reference_number
            """,
            new { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, ReferenceNumber = referenceNumber },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private async Task<IReadOnlyList<AccountReportRow>> QueryAccountsAsync(
        string sql, long accountingBookId, long fiscalYearId, DateOnly? fromDate, DateOnly toDate,
        IReadOnlyList<string> excludedDocumentTypes, CancellationToken cancellationToken)
    {
        await using var shard = await OpenAsync(fiscalYearId, cancellationToken);
        var rows = await shard.Connection.QueryAsync<AccountReportRow>(new CommandDefinition(
            sql,
            new
            {
                AccountingBookId = accountingBookId,
                FiscalYearId = fiscalYearId,
                FromDate = fromDate,
                ToDate = toDate,
                HasExcludedTypes = excludedDocumentTypes.Count > 0,
                ExcludedDocumentTypes = excludedDocumentTypes
            }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private Task<IShardConnection> OpenAsync(long fiscalYearId, CancellationToken cancellationToken) =>
        connectionFactory.OpenAsync(shardKeyFactory.Create(fiscalYearId), cancellationToken: cancellationToken);

    private const string TrialBalanceSql = """
        SELECT @FiscalYearId AS FiscalYearId, account_class_code AS AccountClassCode,
            general_account_code AS GeneralAccountCode,
            subsidiary_account_code AS SubsidiaryAccountCode,
            detail_account_code AS DetailAccountCode,
            SUM(CASE WHEN balance_date < @FromDate THEN debit_turnover - credit_turnover ELSE 0 END) AS OpeningBalance,
            SUM(CASE WHEN balance_date >= @FromDate THEN debit_turnover ELSE 0 END) AS DebitTurnover,
            SUM(CASE WHEN balance_date >= @FromDate THEN credit_turnover ELSE 0 END) AS CreditTurnover,
            SUM(debit_turnover - credit_turnover) AS ClosingBalance
        FROM daily_account_turnover
        WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
          AND balance_date <= @ToDate
          AND (@HasExcludedTypes = 0 OR document_type NOT IN @ExcludedDocumentTypes)
        GROUP BY account_class_code, general_account_code, subsidiary_account_code, detail_account_code
        ORDER BY account_class_code, general_account_code, subsidiary_account_code, detail_account_code;
        """;

    private const string BalanceAsOfSql = """
        SELECT @FiscalYearId AS FiscalYearId, account_class_code AS AccountClassCode,
            general_account_code AS GeneralAccountCode,
            subsidiary_account_code AS SubsidiaryAccountCode,
            detail_account_code AS DetailAccountCode,
            CAST(0 AS DECIMAL(19,4)) AS OpeningBalance,
            CAST(0 AS DECIMAL(19,4)) AS DebitTurnover,
            CAST(0 AS DECIMAL(19,4)) AS CreditTurnover,
            SUM(net_change) AS ClosingBalance
        FROM daily_account_balance
        WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
          AND balance_date <= @ToDate
        GROUP BY account_class_code, general_account_code, subsidiary_account_code, detail_account_code
        ORDER BY account_class_code, general_account_code, subsidiary_account_code, detail_account_code;
        """;

    private const string TurnoverSql = """
        SELECT @FiscalYearId AS FiscalYearId, account_class_code AS AccountClassCode,
            general_account_code AS GeneralAccountCode,
            subsidiary_account_code AS SubsidiaryAccountCode,
            detail_account_code AS DetailAccountCode,
            CAST(0 AS DECIMAL(19,4)) AS OpeningBalance,
            SUM(debit_turnover) AS DebitTurnover,
            SUM(credit_turnover) AS CreditTurnover,
            SUM(debit_turnover - credit_turnover) AS ClosingBalance
        FROM daily_account_turnover
        WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
          AND balance_date >= @FromDate AND balance_date <= @ToDate
          AND (@HasExcludedTypes = 0 OR document_type NOT IN @ExcludedDocumentTypes)
        GROUP BY account_class_code, general_account_code, subsidiary_account_code, detail_account_code
        ORDER BY account_class_code, general_account_code, subsidiary_account_code, detail_account_code;
        """;

    private const string TransactionSql = """
        SELECT entry.id AS EntryId, entry.accounting_book_id AS AccountingBookId,
            entry.fiscal_year_id AS FiscalYearId, entry.reference_number AS ReferenceNumber,
            entry.journal_entry_number AS JournalEntryNumber, entry.accounting_date AS AccountingDate,
            entry.registered_at AS RegisteredAt, entry.description AS EntryDescription,
            entry.document_type AS DocumentType, entry.insertion_type AS InsertionType,
            entry.balance_effect AS BalanceEffect,
            entry.reversal_of_reference_number AS ReversalOfReferenceNumber,
            entry.reversed_by_reference_number AS ReversedByReferenceNumber,
            entry.reversal_reason AS ReversalReason,
            line.row_number AS RowNumber, line.account_class_code AS AccountClassCode,
            line.general_account_code AS GeneralAccountCode,
            line.subsidiary_account_code AS SubsidiaryAccountCode,
            ISNULL(line.detail_account_code, '') AS DetailAccountCode,
            line.side AS Side, line.amount AS Amount, line.description AS LineDescription
        FROM journal_entry entry
        INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
        WHERE entry.accounting_book_id = @AccountingBookId AND entry.fiscal_year_id = @FiscalYearId
          AND entry.status = 'POSTED'
          AND (@FromDate IS NULL OR entry.accounting_date >= @FromDate)
          AND (@ToDate IS NULL OR entry.accounting_date <= @ToDate)
          AND (@AccountClassCode IS NULL OR line.account_class_code = @AccountClassCode)
          AND (@GeneralAccountCode IS NULL OR line.general_account_code = @GeneralAccountCode)
          AND (@SubsidiaryAccountCode IS NULL OR line.subsidiary_account_code = @SubsidiaryAccountCode)
          AND (@DetailAccountCode IS NULL OR line.detail_account_code = @DetailAccountCode)
        ORDER BY entry.accounting_date, entry.registered_at, entry.reference_number, line.row_number
        OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;
}
