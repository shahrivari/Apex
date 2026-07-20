using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryProjectionReadRepository(
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory) : IJournalEntryProjectionReadRepository
{
    public async Task<DailyAccountTurnoverRow?> GetTurnoverAsync(
        long accountingBookId, long fiscalYearId, DateOnly balanceDate,
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        string? detailAccountCode, string documentType, CancellationToken cancellationToken = default)
    {
        await using var shard = await OpenAsync(fiscalYearId, cancellationToken);
        return await shard.Connection.QuerySingleOrDefaultAsync<DailyAccountTurnoverRow>(new CommandDefinition(
            """
            SELECT
                debit_turnover AS DebitTurnover,
                credit_turnover AS CreditTurnover,
                net_turnover AS NetTurnover,
                updated_at AS UpdatedAt,
                projection_version AS ProjectionVersion
            FROM daily_account_turnover
            WHERE accounting_book_id = @AccountingBookId
              AND fiscal_year_id = @FiscalYearId
              AND balance_date = @BalanceDate
              AND account_class_code = @AccountClassCode
              AND general_account_code = @GeneralAccountCode
              AND subsidiary_account_code = @SubsidiaryAccountCode
              AND detail_account_code = @DetailAccountCode
              AND document_type = @DocumentType
            """,
            new
            {
                AccountingBookId = accountingBookId,
                FiscalYearId = fiscalYearId,
                BalanceDate = balanceDate,
                AccountClassCode = accountClassCode,
                GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode,
                DetailAccountCode = detailAccountCode ?? string.Empty,
                DocumentType = documentType
            }, cancellationToken: cancellationToken));
    }

    public async Task<decimal> GetClosingBalanceAsOfAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate,
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        string? detailAccountCode, CancellationToken cancellationToken = default)
    {
        await using var shard = await OpenAsync(fiscalYearId, cancellationToken);
        return await shard.Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            SELECT ISNULL(SUM(net_change), 0)
            FROM daily_account_balance
            WHERE accounting_book_id = @AccountingBookId
              AND fiscal_year_id = @FiscalYearId
              AND account_class_code = @AccountClassCode
              AND general_account_code = @GeneralAccountCode
              AND subsidiary_account_code = @SubsidiaryAccountCode
              AND detail_account_code = @DetailAccountCode
              AND balance_date <= @AsOfDate
            """,
            new
            {
                AccountingBookId = accountingBookId,
                FiscalYearId = fiscalYearId,
                AccountClassCode = accountClassCode,
                GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode,
                DetailAccountCode = detailAccountCode ?? string.Empty,
                AsOfDate = asOfDate
            }, cancellationToken: cancellationToken));
    }

    private Task<IShardConnection> OpenAsync(long fiscalYearId, CancellationToken cancellationToken) =>
        shardConnectionFactory.OpenAsync(shardKeyFactory.Create(fiscalYearId), beginTransaction: false, cancellationToken);
}
