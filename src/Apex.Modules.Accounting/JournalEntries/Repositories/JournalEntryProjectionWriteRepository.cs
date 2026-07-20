using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryProjectionWriteRepository : IJournalEntryProjectionWriteRepository
{
    public const int ProjectionVersion = 1;

    public async Task ApplyPostingAsync(
        IShardConnection connection, JournalEntry entry, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        foreach (var line in entry.Lines)
        {
            var isDebit = line.Side == JournalEntrySide.Debit;
            var parameters = new
            {
                entry.AccountingBookId,
                entry.FiscalYearId,
                BalanceDate = entry.AccountingDate,
                line.AccountClassCode,
                line.GeneralAccountCode,
                line.SubsidiaryAccountCode,
                DetailAccountCode = line.DetailAccountCode ?? string.Empty,
                DocumentType = entry.DocumentType.ToDatabaseValue(),
                DebitDelta = isDebit ? line.Amount : 0m,
                CreditDelta = isDebit ? 0m : line.Amount,
                NetDelta = isDebit ? line.Amount : -line.Amount,
                UpdatedAt = updatedAt,
                Version = ProjectionVersion
            };

            await connection.Connection.ExecuteAsync(new CommandDefinition(
                TurnoverMergeSql, parameters, connection.Transaction, cancellationToken: cancellationToken));
            await connection.Connection.ExecuteAsync(new CommandDefinition(
                BalanceMergeSql, parameters, connection.Transaction, cancellationToken: cancellationToken));
        }
    }

    private const string TurnoverMergeSql = """
        MERGE daily_account_turnover WITH (HOLDLOCK) AS t
        USING (VALUES (
            @AccountingBookId, @FiscalYearId, @BalanceDate, @AccountClassCode, @GeneralAccountCode,
            @SubsidiaryAccountCode, @DetailAccountCode, @DocumentType)) AS s (
            accounting_book_id, fiscal_year_id, balance_date, account_class_code, general_account_code,
            subsidiary_account_code, detail_account_code, document_type)
            ON t.accounting_book_id = s.accounting_book_id
           AND t.fiscal_year_id = s.fiscal_year_id
           AND t.balance_date = s.balance_date
           AND t.account_class_code = s.account_class_code
           AND t.general_account_code = s.general_account_code
           AND t.subsidiary_account_code = s.subsidiary_account_code
           AND t.detail_account_code = s.detail_account_code
           AND t.document_type = s.document_type
        WHEN MATCHED THEN UPDATE SET
            debit_turnover = t.debit_turnover + @DebitDelta,
            credit_turnover = t.credit_turnover + @CreditDelta,
            updated_at = @UpdatedAt,
            projection_version = @Version
        WHEN NOT MATCHED THEN INSERT (
            accounting_book_id, fiscal_year_id, balance_date, account_class_code, general_account_code,
            subsidiary_account_code, detail_account_code, document_type, debit_turnover, credit_turnover,
            updated_at, projection_version)
            VALUES (s.accounting_book_id, s.fiscal_year_id, s.balance_date, s.account_class_code,
            s.general_account_code, s.subsidiary_account_code, s.detail_account_code, s.document_type,
            @DebitDelta, @CreditDelta, @UpdatedAt, @Version);
        """;

    private const string BalanceMergeSql = """
        MERGE daily_account_balance WITH (HOLDLOCK) AS t
        USING (VALUES (
            @AccountingBookId, @FiscalYearId, @AccountClassCode, @GeneralAccountCode,
            @SubsidiaryAccountCode, @DetailAccountCode, @BalanceDate)) AS s (
            accounting_book_id, fiscal_year_id, account_class_code, general_account_code,
            subsidiary_account_code, detail_account_code, balance_date)
            ON t.accounting_book_id = s.accounting_book_id
           AND t.fiscal_year_id = s.fiscal_year_id
           AND t.account_class_code = s.account_class_code
           AND t.general_account_code = s.general_account_code
           AND t.subsidiary_account_code = s.subsidiary_account_code
           AND t.detail_account_code = s.detail_account_code
           AND t.balance_date = s.balance_date
        WHEN MATCHED THEN UPDATE SET
            net_change = t.net_change + @NetDelta,
            updated_at = @UpdatedAt,
            projection_version = @Version
        WHEN NOT MATCHED THEN INSERT (
            accounting_book_id, fiscal_year_id, account_class_code, general_account_code,
            subsidiary_account_code, detail_account_code, balance_date, net_change, updated_at, projection_version)
            VALUES (s.accounting_book_id, s.fiscal_year_id, s.account_class_code, s.general_account_code,
            s.subsidiary_account_code, s.detail_account_code, s.balance_date, @NetDelta, @UpdatedAt, @Version);
        """;
}
