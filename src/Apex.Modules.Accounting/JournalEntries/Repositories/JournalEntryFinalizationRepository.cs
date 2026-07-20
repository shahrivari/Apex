using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryFinalizationRepository : IJournalEntryFinalizationRepository
{
    public async Task<bool> HasBlockingDraftsAsync(
        IShardConnection connection, long fiscalYearId, DateOnly afterDate, DateOnly throughDate,
        CancellationToken cancellationToken = default) =>
        await connection.Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM journal_entry WITH (UPDLOCK, HOLDLOCK) WHERE fiscal_year_id = @FiscalYearId AND status = 'DRAFT' AND accounting_date > @AfterDate AND accounting_date <= @ThroughDate",
            new { FiscalYearId = fiscalYearId, AfterDate = afterDate, ThroughDate = throughDate },
            connection.Transaction, cancellationToken: cancellationToken)) > 0;

    public async Task<bool> AreProjectionsReconciledAsync(
        IShardConnection connection, long accountingBookId, long fiscalYearId, DateOnly throughDate,
        CancellationToken cancellationToken = default)
    {
        var mismatches = await connection.Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            ReconciliationSql,
            new
            {
                AccountingBookId = accountingBookId,
                FiscalYearId = fiscalYearId,
                ThroughDate = throughDate
            }, connection.Transaction, cancellationToken: cancellationToken));
        return mismatches == 0;
    }

    public async Task RenumberAndFinalizeAsync(
        IShardConnection connection, long fiscalYearId, DateOnly throughDate,
        long temporaryNumberBase, CancellationToken cancellationToken = default)
    {
        var tailCount = await connection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT_BIG(1) FROM journal_entry WITH (UPDLOCK, HOLDLOCK) WHERE fiscal_year_id = @FiscalYearId AND number_finalized = 0",
            new { FiscalYearId = fiscalYearId }, connection.Transaction,
            cancellationToken: cancellationToken));
        if (tailCount > long.MaxValue - temporaryNumberBase)
            throw new ConflictException(
                "The unfinalized journal entry tail cannot be renumbered.",
                JournalEntryErrors.NumberingConflict);

        var finalizedMaximum = await connection.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT ISNULL(MAX(journal_entry_number), 0) FROM journal_entry WHERE fiscal_year_id = @FiscalYearId AND number_finalized = 1",
            new { FiscalYearId = fiscalYearId }, connection.Transaction,
            cancellationToken: cancellationToken));

        await connection.Connection.ExecuteAsync(new CommandDefinition(
            TemporaryRenumberSql,
            new { FiscalYearId = fiscalYearId, TemporaryNumberBase = temporaryNumberBase },
            connection.Transaction, cancellationToken: cancellationToken));
        await connection.Connection.ExecuteAsync(new CommandDefinition(
            FinalRenumberSql,
            new
            {
                FiscalYearId = fiscalYearId,
                FinalizedMaximum = finalizedMaximum,
                ThroughDate = throughDate
            }, connection.Transaction, cancellationToken: cancellationToken));
    }

    private const string TemporaryRenumberSql = """
        WITH ordered AS (
            SELECT id, ROW_NUMBER() OVER (
                ORDER BY accounting_date, registered_at, reference_number) AS row_number
            FROM journal_entry
            WHERE fiscal_year_id = @FiscalYearId AND number_finalized = 0
        )
        UPDATE entry
        SET journal_entry_number = @TemporaryNumberBase + ordered.row_number
        FROM journal_entry entry
        INNER JOIN ordered ON ordered.id = entry.id;
        """;

    private const string FinalRenumberSql = """
        WITH ordered AS (
            SELECT id, accounting_date, status, ROW_NUMBER() OVER (
                ORDER BY accounting_date, registered_at, reference_number) AS row_number
            FROM journal_entry
            WHERE fiscal_year_id = @FiscalYearId AND number_finalized = 0
        )
        UPDATE entry
        SET journal_entry_number = @FinalizedMaximum + ordered.row_number,
            number_finalized = CASE
                WHEN ordered.status = 'POSTED' AND ordered.accounting_date <= @ThroughDate THEN 1
                ELSE 0
            END
        FROM journal_entry entry
        INNER JOIN ordered ON ordered.id = entry.id;
        """;

    private const string ReconciliationSql = """
        WITH source_turnover AS (
            SELECT entry.accounting_book_id, entry.fiscal_year_id,
                entry.accounting_date AS balance_date,
                line.account_class_code, line.general_account_code,
                line.subsidiary_account_code,
                ISNULL(line.detail_account_code, '') AS detail_account_code,
                entry.document_type,
                SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE 0 END) AS debit_turnover,
                SUM(CASE WHEN line.side = 'CREDIT' THEN line.amount ELSE 0 END) AS credit_turnover
            FROM journal_entry entry
            INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
            WHERE entry.accounting_book_id = @AccountingBookId
              AND entry.fiscal_year_id = @FiscalYearId
              AND entry.status = 'POSTED'
              AND entry.balance_effect = 'FINANCIAL'
              AND entry.accounting_date <= @ThroughDate
            GROUP BY entry.accounting_book_id, entry.fiscal_year_id, entry.accounting_date,
                line.account_class_code, line.general_account_code, line.subsidiary_account_code,
                ISNULL(line.detail_account_code, ''), entry.document_type
        ), projection_turnover AS (
            SELECT * FROM daily_account_turnover
            WHERE accounting_book_id = @AccountingBookId
              AND fiscal_year_id = @FiscalYearId
              AND balance_date <= @ThroughDate
        ), turnover_mismatch AS (
            SELECT 1 AS mismatch
            FROM source_turnover source
            FULL OUTER JOIN projection_turnover projection
              ON projection.accounting_book_id = source.accounting_book_id
             AND projection.fiscal_year_id = source.fiscal_year_id
             AND projection.balance_date = source.balance_date
             AND projection.account_class_code = source.account_class_code
             AND projection.general_account_code = source.general_account_code
             AND projection.subsidiary_account_code = source.subsidiary_account_code
             AND projection.detail_account_code = source.detail_account_code
             AND projection.document_type = source.document_type
            WHERE source.accounting_book_id IS NULL
               OR projection.accounting_book_id IS NULL
               OR source.debit_turnover <> projection.debit_turnover
               OR source.credit_turnover <> projection.credit_turnover
        ), source_balance AS (
            SELECT entry.accounting_book_id, entry.fiscal_year_id,
                entry.accounting_date AS balance_date,
                line.account_class_code, line.general_account_code,
                line.subsidiary_account_code,
                ISNULL(line.detail_account_code, '') AS detail_account_code,
                SUM(CASE WHEN line.side = 'DEBIT' THEN line.amount ELSE -line.amount END) AS net_change
            FROM journal_entry entry
            INNER JOIN journal_entry_line line ON line.journal_entry_id = entry.id
            WHERE entry.accounting_book_id = @AccountingBookId
              AND entry.fiscal_year_id = @FiscalYearId
              AND entry.status = 'POSTED'
              AND entry.balance_effect = 'FINANCIAL'
              AND entry.accounting_date <= @ThroughDate
            GROUP BY entry.accounting_book_id, entry.fiscal_year_id, entry.accounting_date,
                line.account_class_code, line.general_account_code, line.subsidiary_account_code,
                ISNULL(line.detail_account_code, '')
        ), projection_balance AS (
            SELECT * FROM daily_account_balance
            WHERE accounting_book_id = @AccountingBookId
              AND fiscal_year_id = @FiscalYearId
              AND balance_date <= @ThroughDate
        ), balance_mismatch AS (
            SELECT 1 AS mismatch
            FROM source_balance source
            FULL OUTER JOIN projection_balance projection
              ON projection.accounting_book_id = source.accounting_book_id
             AND projection.fiscal_year_id = source.fiscal_year_id
             AND projection.balance_date = source.balance_date
             AND projection.account_class_code = source.account_class_code
             AND projection.general_account_code = source.general_account_code
             AND projection.subsidiary_account_code = source.subsidiary_account_code
             AND projection.detail_account_code = source.detail_account_code
            WHERE source.accounting_book_id IS NULL
               OR projection.accounting_book_id IS NULL
               OR source.net_change <> projection.net_change
        )
        SELECT CASE WHEN EXISTS (SELECT 1 FROM turnover_mismatch)
            OR EXISTS (SELECT 1 FROM balance_mismatch) THEN 1 ELSE 0 END;
        """;
}
