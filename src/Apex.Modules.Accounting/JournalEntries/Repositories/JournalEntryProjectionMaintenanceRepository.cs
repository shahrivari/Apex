using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryProjectionMaintenanceRepository
    : IJournalEntryProjectionMaintenanceRepository
{
    public async Task RebuildAsync(
        IShardConnection connection, long accountingBookId, long fiscalYearId,
        DateOnly? fromDate, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            AccountingBookId = accountingBookId,
            FiscalYearId = fiscalYearId,
            FromDate = fromDate,
            UpdatedAt = updatedAt,
            ProjectionVersion = JournalEntryProjectionWriteRepository.ProjectionVersion
        };
        await connection.Connection.ExecuteAsync(new CommandDefinition(
            DeleteSql, parameters, connection.Transaction, cancellationToken: cancellationToken));
        await connection.Connection.ExecuteAsync(new CommandDefinition(
            RebuildTurnoverSql, parameters, connection.Transaction, cancellationToken: cancellationToken));
        await connection.Connection.ExecuteAsync(new CommandDefinition(
            RebuildBalanceSql, parameters, connection.Transaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProjectionMismatchRow>> ReconcileAsync(
        IShardConnection connection, long accountingBookId, long fiscalYearId,
        CancellationToken cancellationToken = default) =>
        (await connection.Connection.QueryAsync<ProjectionMismatchRow>(new CommandDefinition(
            ReconcileSql,
            new { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId },
            connection.Transaction, cancellationToken: cancellationToken))).AsList();

    private const string DeleteSql = """
        DELETE FROM daily_account_turnover
        WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
          AND (@FromDate IS NULL OR balance_date >= @FromDate);
        DELETE FROM daily_account_balance
        WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
          AND (@FromDate IS NULL OR balance_date >= @FromDate);
        """;

    private const string RebuildTurnoverSql = """
        INSERT INTO daily_account_turnover (
            accounting_book_id, fiscal_year_id, balance_date, account_class_code,
            general_account_code, subsidiary_account_code, detail_account_code,
            document_type, debit_turnover, credit_turnover, updated_at, projection_version)
        SELECT entry.accounting_book_id, entry.fiscal_year_id, entry.accounting_date,
            line.account_class_code, line.general_account_code, line.subsidiary_account_code,
            ISNULL(line.detail_account_code, ''), entry.document_type,
            SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE 0 END),
            SUM(CASE WHEN line.side = 'CREDIT' THEN line.amount ELSE 0 END),
            @UpdatedAt, @ProjectionVersion
        FROM journal_entry entry
        INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
        WHERE entry.accounting_book_id = @AccountingBookId
          AND entry.fiscal_year_id = @FiscalYearId
          AND entry.status = 'POSTED' AND entry.balance_effect = 'FINANCIAL'
          AND (@FromDate IS NULL OR entry.accounting_date >= @FromDate)
        GROUP BY entry.accounting_book_id, entry.fiscal_year_id, entry.accounting_date,
            line.account_class_code, line.general_account_code, line.subsidiary_account_code,
            ISNULL(line.detail_account_code, ''), entry.document_type;
        """;

    private const string RebuildBalanceSql = """
        INSERT INTO daily_account_balance (
            accounting_book_id, fiscal_year_id, account_class_code, general_account_code,
            subsidiary_account_code, detail_account_code, balance_date, net_change,
            updated_at, projection_version)
        SELECT entry.accounting_book_id, entry.fiscal_year_id,
            line.account_class_code, line.general_account_code, line.subsidiary_account_code,
            ISNULL(line.detail_account_code, ''), entry.accounting_date,
            SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE -line.amount END),
            @UpdatedAt, @ProjectionVersion
        FROM journal_entry entry
        INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
        WHERE entry.accounting_book_id = @AccountingBookId
          AND entry.fiscal_year_id = @FiscalYearId
          AND entry.status = 'POSTED' AND entry.balance_effect = 'FINANCIAL'
          AND (@FromDate IS NULL OR entry.accounting_date >= @FromDate)
        GROUP BY entry.accounting_book_id, entry.fiscal_year_id, entry.accounting_date,
            line.account_class_code, line.general_account_code, line.subsidiary_account_code,
            ISNULL(line.detail_account_code, '')
        HAVING SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE -line.amount END) <> 0;
        """;

    private const string ReconcileSql = """
        WITH source_turnover AS (
            SELECT entry.accounting_date AS balance_date, line.account_class_code,
                line.general_account_code, line.subsidiary_account_code,
                ISNULL(line.detail_account_code, '') AS detail_account_code, entry.document_type,
                SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE 0 END) AS debit_turnover,
                SUM(CASE WHEN line.side = 'CREDIT' THEN line.amount ELSE 0 END) AS credit_turnover
            FROM journal_entry entry
            INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
            WHERE entry.accounting_book_id = @AccountingBookId
              AND entry.fiscal_year_id = @FiscalYearId
              AND entry.status = 'POSTED' AND entry.balance_effect = 'FINANCIAL'
            GROUP BY entry.accounting_date, line.account_class_code, line.general_account_code,
                line.subsidiary_account_code, ISNULL(line.detail_account_code, ''), entry.document_type
        ), turnover AS (
            SELECT balance_date, account_class_code, general_account_code,
                subsidiary_account_code, detail_account_code, document_type,
                debit_turnover, credit_turnover
            FROM daily_account_turnover
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
        ), turnover_diff AS (
            SELECT 'TURNOVER' AS Projection,
                COALESCE(source.balance_date, actual.balance_date) AS BalanceDate,
                COALESCE(source.account_class_code, actual.account_class_code) AS AccountClassCode,
                COALESCE(source.general_account_code, actual.general_account_code) AS GeneralAccountCode,
                COALESCE(source.subsidiary_account_code, actual.subsidiary_account_code) AS SubsidiaryAccountCode,
                COALESCE(source.detail_account_code, actual.detail_account_code) AS DetailAccountCode,
                COALESCE(source.document_type, actual.document_type) AS DocumentType,
                source.debit_turnover AS ExpectedDebit, actual.debit_turnover AS ActualDebit,
                source.credit_turnover AS ExpectedCredit, actual.credit_turnover AS ActualCredit,
                ISNULL(source.debit_turnover, 0) - ISNULL(source.credit_turnover, 0) AS ExpectedNet,
                ISNULL(actual.debit_turnover, 0) - ISNULL(actual.credit_turnover, 0) AS ActualNet
            FROM source_turnover source
            FULL OUTER JOIN turnover actual ON actual.balance_date = source.balance_date
             AND actual.account_class_code = source.account_class_code
             AND actual.general_account_code = source.general_account_code
             AND actual.subsidiary_account_code = source.subsidiary_account_code
             AND actual.detail_account_code = source.detail_account_code
             AND actual.document_type = source.document_type
            WHERE source.balance_date IS NULL OR actual.balance_date IS NULL
               OR source.debit_turnover <> actual.debit_turnover
               OR source.credit_turnover <> actual.credit_turnover
        ), source_balance AS (
            SELECT entry.accounting_date AS balance_date, line.account_class_code,
                line.general_account_code, line.subsidiary_account_code,
                ISNULL(line.detail_account_code, '') AS detail_account_code,
                SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE -line.amount END) AS net_change
            FROM journal_entry entry
            INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
            WHERE entry.accounting_book_id = @AccountingBookId
              AND entry.fiscal_year_id = @FiscalYearId
              AND entry.status = 'POSTED' AND entry.balance_effect = 'FINANCIAL'
            GROUP BY entry.accounting_date, line.account_class_code, line.general_account_code,
                line.subsidiary_account_code, ISNULL(line.detail_account_code, '')
            HAVING SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE -line.amount END) <> 0
        ), balance AS (
            SELECT balance_date, account_class_code, general_account_code,
                subsidiary_account_code, detail_account_code, net_change
            FROM daily_account_balance
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
        ), balance_diff AS (
            SELECT 'BALANCE' AS Projection,
                COALESCE(source.balance_date, actual.balance_date) AS BalanceDate,
                COALESCE(source.account_class_code, actual.account_class_code) AS AccountClassCode,
                COALESCE(source.general_account_code, actual.general_account_code) AS GeneralAccountCode,
                COALESCE(source.subsidiary_account_code, actual.subsidiary_account_code) AS SubsidiaryAccountCode,
                COALESCE(source.detail_account_code, actual.detail_account_code) AS DetailAccountCode,
                CAST(NULL AS VARCHAR(32)) AS DocumentType,
                CAST(NULL AS DECIMAL(19,4)) AS ExpectedDebit,
                CAST(NULL AS DECIMAL(19,4)) AS ActualDebit,
                CAST(NULL AS DECIMAL(19,4)) AS ExpectedCredit,
                CAST(NULL AS DECIMAL(19,4)) AS ActualCredit,
                ISNULL(source.net_change, 0) AS ExpectedNet,
                ISNULL(actual.net_change, 0) AS ActualNet
            FROM source_balance source
            FULL OUTER JOIN balance actual ON actual.balance_date = source.balance_date
             AND actual.account_class_code = source.account_class_code
             AND actual.general_account_code = source.general_account_code
             AND actual.subsidiary_account_code = source.subsidiary_account_code
             AND actual.detail_account_code = source.detail_account_code
            WHERE source.balance_date IS NULL OR actual.balance_date IS NULL
               OR source.net_change <> actual.net_change
        )
        SELECT Projection, BalanceDate, AccountClassCode, GeneralAccountCode,
            SubsidiaryAccountCode, DetailAccountCode, DocumentType,
            ExpectedDebit, ActualDebit, ExpectedCredit, ActualCredit, ExpectedNet, ActualNet
        FROM turnover_diff
        UNION ALL
        SELECT Projection, BalanceDate, AccountClassCode, GeneralAccountCode,
            SubsidiaryAccountCode, DetailAccountCode, DocumentType,
            ExpectedDebit, ActualDebit, ExpectedCredit, ActualCredit, ExpectedNet, ActualNet
        FROM balance_diff
        ORDER BY BalanceDate, AccountClassCode, GeneralAccountCode,
            SubsidiaryAccountCode, DetailAccountCode, Projection;
        """;
}
