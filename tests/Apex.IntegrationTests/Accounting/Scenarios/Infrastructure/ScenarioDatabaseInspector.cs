using Dapper;
using Microsoft.Data.SqlClient;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>Authoritative Journal Entry header row, read directly from <c>journal_entry</c>.</summary>
public sealed record JournalEntryHeaderRow(
    long Id, long AccountingBookId, long FiscalYearId, long ReferenceNumber, long JournalEntryNumber,
    bool NumberFinalized, DateOnly AccountingDate, DateTime RegisteredAt, string Description,
    string DocumentType, string InsertionType, string Status, string BalanceEffect,
    string? SourceType, string? SourceReference, long? ReversalOfReferenceNumber,
    long? ReversedByReferenceNumber, string? ReversalReason, DateTime? PostedAt,
    DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>Authoritative Journal Entry line row, read directly from <c>journal_entry_line</c>.</summary>
public sealed record ScenarioJournalEntryLineRow(
    long Id, long JournalEntryId, int RowNumber, string AccountClassCode, string GeneralAccountCode,
    string SubsidiaryAccountCode, string? DetailAccountCode, string Side, decimal Amount, string Description);

/// <summary>Projection row from <c>daily_account_turnover</c> (one row per document type per day).</summary>
public sealed record DailyTurnoverRow(
    long AccountingBookId, long FiscalYearId, DateOnly BalanceDate, string AccountClassCode,
    string GeneralAccountCode, string SubsidiaryAccountCode, string DetailAccountCode, string DocumentType,
    decimal DebitTurnover, decimal CreditTurnover, decimal NetTurnover, int ProjectionVersion);

/// <summary>Projection row from <c>daily_account_balance</c> (financial document types only, per day).</summary>
public sealed record DailyBalanceRow(
    long AccountingBookId, long FiscalYearId, string AccountClassCode, string GeneralAccountCode,
    string SubsidiaryAccountCode, string DetailAccountCode, DateOnly BalanceDate, decimal NetChange,
    int ProjectionVersion);

/// <summary>Fiscal Year numbering counters and finalization boundary, read directly from <c>fiscal_year</c>.</summary>
public sealed record FiscalYearCounters(
    long NextReferenceNumber, long NextJournalEntryNumber, DateOnly FinalizedThroughDate);

/// <summary>Debit/credit movement recomputed directly from posted financial lines, independent of
/// any production report or projection code.</summary>
public sealed record AuthoritativeMovement(decimal Debit, decimal Credit)
{
    public decimal Net => Debit - Credit;
}

/// <summary>Aggregated debit/credit turnover for one grain over a date range.</summary>
public sealed record TurnoverAggregate(decimal Debit, decimal Credit)
{
    public decimal Net => Debit - Credit;
}

/// <summary>
/// A cheap, comparable fingerprint of a Fiscal Year's authoritative and projection state, used to
/// prove that a rejected write left no partial trace (spec §7: "No unexpected counter changes...
/// No projection changes... Previously committed balances remain unchanged").
/// </summary>
public sealed record FiscalYearSnapshot(
    int EntryCount, int LineCount, long NextReferenceNumber, long NextJournalEntryNumber,
    decimal TurnoverChecksum, decimal BalanceChecksum);

/// <summary>
/// Narrowly scoped, read-only Dapper queries against the shard database. Every query uses fixed
/// table/column names and parameters — never derives SQL identifiers from input (spec §6.2, §12.5).
/// Used only to verify authoritative/derived state independently of the public API; ordinary
/// scenario setup goes through <see cref="ScenarioApiClient"/> instead.
/// </summary>
public sealed class ScenarioDatabaseInspector(string shardConnectionString)
{
    public async Task<string> GetShardMarkerAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT TOP (1) name FROM shard_marker",
            cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("The test shard marker is missing.");
    }

    public async Task CorruptTurnoverAsync(
        long accountingBookId, long fiscalYearId, DateOnly balanceDate, string accountClassCode,
        string generalAccountCode, string subsidiaryAccountCode, string? detailAccountCode,
        string documentType, decimal debitTurnover, decimal creditTurnover,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            UPDATE daily_account_turnover
            SET debit_turnover = @DebitTurnover,
                credit_turnover = @CreditTurnover
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND balance_date = @BalanceDate AND account_class_code = @AccountClassCode
              AND general_account_code = @GeneralAccountCode
              AND subsidiary_account_code = @SubsidiaryAccountCode
              AND detail_account_code = @DetailAccountCode AND document_type = @DocumentType
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, BalanceDate = balanceDate,
                AccountClassCode = accountClassCode, GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode, DetailAccountCode = detailAccountCode ?? "",
                DocumentType = documentType, DebitTurnover = debitTurnover, CreditTurnover = creditTurnover
            },
            cancellationToken: cancellationToken);
        Assert.Equal(1, await connection.ExecuteAsync(command));
    }

    public async Task CorruptBalanceAsync(
        long accountingBookId, long fiscalYearId, DateOnly balanceDate, string accountClassCode,
        string generalAccountCode, string subsidiaryAccountCode, string? detailAccountCode,
        decimal netChange, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            UPDATE daily_account_balance
            SET net_change = @NetChange
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND balance_date = @BalanceDate AND account_class_code = @AccountClassCode
              AND general_account_code = @GeneralAccountCode
              AND subsidiary_account_code = @SubsidiaryAccountCode
              AND detail_account_code = @DetailAccountCode
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, BalanceDate = balanceDate,
                AccountClassCode = accountClassCode, GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode, DetailAccountCode = detailAccountCode ?? "",
                NetChange = netChange
            },
            cancellationToken: cancellationToken);
        Assert.Equal(1, await connection.ExecuteAsync(command));
    }

    public async Task<JournalEntryHeaderRow?> GetHeaderByReferenceAsync(
        long fiscalYearId, long referenceNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT id AS Id, accounting_book_id AS AccountingBookId, fiscal_year_id AS FiscalYearId,
                   reference_number AS ReferenceNumber, journal_entry_number AS JournalEntryNumber,
                   number_finalized AS NumberFinalized, accounting_date AS AccountingDate,
                   registered_at AS RegisteredAt, description AS Description, document_type AS DocumentType,
                   insertion_type AS InsertionType, status AS Status, balance_effect AS BalanceEffect,
                   source_type AS SourceType, source_reference AS SourceReference,
                   reversal_of_reference_number AS ReversalOfReferenceNumber,
                   reversed_by_reference_number AS ReversedByReferenceNumber, reversal_reason AS ReversalReason,
                   posted_at AS PostedAt, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM journal_entry
            WHERE fiscal_year_id = @FiscalYearId AND reference_number = @ReferenceNumber
            """,
            new { FiscalYearId = fiscalYearId, ReferenceNumber = referenceNumber },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<JournalEntryHeaderRow>(command);
    }

    public async Task<IReadOnlyList<ScenarioJournalEntryLineRow>> GetOrderedLinesAsync(
        long journalEntryId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT id AS Id, journal_entry_id AS JournalEntryId, row_number AS RowNumber,
                   account_class_code AS AccountClassCode, general_account_code AS GeneralAccountCode,
                   subsidiary_account_code AS SubsidiaryAccountCode, detail_account_code AS DetailAccountCode,
                   side AS Side, amount AS Amount, description AS Description
            FROM journal_entry_line
            WHERE journal_entry_id = @JournalEntryId
            ORDER BY row_number ASC
            """,
            new { JournalEntryId = journalEntryId },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ScenarioJournalEntryLineRow>(command);
        return rows.ToList();
    }

    public async Task<int> CountEntriesAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            "SELECT COUNT(*) FROM journal_entry WHERE fiscal_year_id = @FiscalYearId",
            new { FiscalYearId = fiscalYearId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CountLinesAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM journal_entry_line l
            JOIN journal_entry e ON e.id = l.journal_entry_id
            WHERE e.fiscal_year_id = @FiscalYearId
            """,
            new { FiscalYearId = fiscalYearId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    /// <summary>All <c>daily_account_turnover</c> rows (one per document type) for one grain and day.</summary>
    public async Task<IReadOnlyList<DailyTurnoverRow>> GetTurnoverRowsAsync(
        long accountingBookId, long fiscalYearId, DateOnly balanceDate, string accountClassCode,
        string generalAccountCode, string subsidiaryAccountCode, string? detailAccountCode = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT accounting_book_id AS AccountingBookId, fiscal_year_id AS FiscalYearId,
                   balance_date AS BalanceDate, account_class_code AS AccountClassCode,
                   general_account_code AS GeneralAccountCode, subsidiary_account_code AS SubsidiaryAccountCode,
                   detail_account_code AS DetailAccountCode, document_type AS DocumentType,
                   debit_turnover AS DebitTurnover, credit_turnover AS CreditTurnover,
                   net_turnover AS NetTurnover, projection_version AS ProjectionVersion
            FROM daily_account_turnover
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND balance_date = @BalanceDate AND account_class_code = @AccountClassCode
              AND general_account_code = @GeneralAccountCode
              AND subsidiary_account_code = @SubsidiaryAccountCode
              AND (@DetailAccountCode IS NULL OR detail_account_code = @DetailAccountCode)
            ORDER BY document_type ASC
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, BalanceDate = balanceDate,
                AccountClassCode = accountClassCode, GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode, DetailAccountCode = detailAccountCode
            },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<DailyTurnoverRow>(command);
        return rows.ToList();
    }

    /// <summary>
    /// Aggregated debit/credit turnover over an inclusive date range. Passing only
    /// <paramref name="accountClassCode"/> rolls descendants up (BAL-010); adding
    /// <paramref name="generalAccountCode"/> and/or <paramref name="subsidiaryAccountCode"/>
    /// narrows the grain (BAL-011/012/013); <paramref name="documentType"/> isolates one
    /// document type (BAL-009). All predicates are parameterized — never string-built.
    /// </summary>
    public async Task<TurnoverAggregate> GetAggregateTurnoverAsync(
        long accountingBookId, long fiscalYearId, DateOnly fromDate, DateOnly toDate, string accountClassCode,
        string? generalAccountCode = null, string? subsidiaryAccountCode = null, string? detailAccountCode = null,
        string? documentType = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT ISNULL(SUM(debit_turnover), 0) AS Debit, ISNULL(SUM(credit_turnover), 0) AS Credit
            FROM daily_account_turnover
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND balance_date BETWEEN @FromDate AND @ToDate
              AND account_class_code = @AccountClassCode
              AND (@GeneralAccountCode IS NULL OR general_account_code = @GeneralAccountCode)
              AND (@SubsidiaryAccountCode IS NULL OR subsidiary_account_code = @SubsidiaryAccountCode)
              AND (@DetailAccountCode IS NULL OR detail_account_code = @DetailAccountCode)
              AND (@DocumentType IS NULL OR document_type = @DocumentType)
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, FromDate = fromDate, ToDate = toDate,
                AccountClassCode = accountClassCode, GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode, DetailAccountCode = detailAccountCode,
                DocumentType = documentType
            },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleAsync<TurnoverAggregate>(command);
    }

    /// <summary>
    /// Closing balance as of <paramref name="asOfDate"/> (SUM of <c>net_change</c> through that
    /// date). Same optional-grain narrowing as <see cref="GetAggregateTurnoverAsync"/>; there is no
    /// document-type column on <c>daily_account_balance</c> because it already excludes statistical
    /// entries by construction.
    /// </summary>
    public async Task<decimal> GetClosingBalanceAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate, string accountClassCode,
        string? generalAccountCode = null, string? subsidiaryAccountCode = null, string? detailAccountCode = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT ISNULL(SUM(net_change), 0)
            FROM daily_account_balance
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND balance_date <= @AsOfDate
              AND account_class_code = @AccountClassCode
              AND (@GeneralAccountCode IS NULL OR general_account_code = @GeneralAccountCode)
              AND (@SubsidiaryAccountCode IS NULL OR subsidiary_account_code = @SubsidiaryAccountCode)
              AND (@DetailAccountCode IS NULL OR detail_account_code = @DetailAccountCode)
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, AsOfDate = asOfDate,
                AccountClassCode = accountClassCode, GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode, DetailAccountCode = detailAccountCode
            },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<decimal>(command);
    }

    public async Task<IReadOnlyList<DailyBalanceRow>> GetBalanceRowsAsync(
        long accountingBookId, long fiscalYearId, string accountClassCode, string generalAccountCode,
        string subsidiaryAccountCode, string? detailAccountCode = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT accounting_book_id AS AccountingBookId, fiscal_year_id AS FiscalYearId,
                   account_class_code AS AccountClassCode, general_account_code AS GeneralAccountCode,
                   subsidiary_account_code AS SubsidiaryAccountCode, detail_account_code AS DetailAccountCode,
                   balance_date AS BalanceDate, net_change AS NetChange, projection_version AS ProjectionVersion
            FROM daily_account_balance
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
              AND account_class_code = @AccountClassCode AND general_account_code = @GeneralAccountCode
              AND subsidiary_account_code = @SubsidiaryAccountCode
              AND (@DetailAccountCode IS NULL OR detail_account_code = @DetailAccountCode)
            ORDER BY balance_date ASC
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, AccountClassCode = accountClassCode,
                GeneralAccountCode = generalAccountCode, SubsidiaryAccountCode = subsidiaryAccountCode,
                DetailAccountCode = detailAccountCode
            },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<DailyBalanceRow>(command);
        return rows.ToList();
    }

    /// <summary>
    /// Recomputes debit/credit movement directly from posted financial lines — independent of any
    /// production report or projection code — for red-path-free comparison against both views.
    /// </summary>
    public async Task<AuthoritativeMovement> ComputeAuthoritativeMovementAsync(
        long accountingBookId, long fiscalYearId, DateOnly asOfDate, string accountClassCode,
        string? generalAccountCode = null, string? subsidiaryAccountCode = null, string? detailAccountCode = null,
        string? documentType = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT
                ISNULL(SUM(CASE WHEN l.side = 'DEBIT' THEN l.amount ELSE 0 END), 0) AS Debit,
                ISNULL(SUM(CASE WHEN l.side = 'CREDIT' THEN l.amount ELSE 0 END), 0) AS Credit
            FROM journal_entry_line l
            JOIN journal_entry e ON e.id = l.journal_entry_id
            WHERE e.accounting_book_id = @AccountingBookId AND e.fiscal_year_id = @FiscalYearId
              AND e.status = 'POSTED' AND e.balance_effect = 'FINANCIAL'
              AND e.accounting_date <= @AsOfDate
              AND l.account_class_code = @AccountClassCode
              AND (@GeneralAccountCode IS NULL OR l.general_account_code = @GeneralAccountCode)
              AND (@SubsidiaryAccountCode IS NULL OR l.subsidiary_account_code = @SubsidiaryAccountCode)
              AND (@DetailAccountCode IS NULL OR ISNULL(l.detail_account_code, '') = @DetailAccountCode)
              AND (@DocumentType IS NULL OR e.document_type = @DocumentType)
            """,
            new
            {
                AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, AsOfDate = asOfDate,
                AccountClassCode = accountClassCode, GeneralAccountCode = generalAccountCode,
                SubsidiaryAccountCode = subsidiaryAccountCode, DetailAccountCode = detailAccountCode,
                DocumentType = documentType
            },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleAsync<AuthoritativeMovement>(command);
    }

    public async Task<FiscalYearCounters> GetFiscalYearCountersAsync(
        long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT next_reference_number AS NextReferenceNumber,
                   next_journal_entry_number AS NextJournalEntryNumber,
                   finalized_through_date AS FinalizedThroughDate
            FROM fiscal_year
            WHERE id = @FiscalYearId
            """,
            new { FiscalYearId = fiscalYearId },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleAsync<FiscalYearCounters>(command);
    }

    /// <summary>
    /// A comparable fingerprint of the Fiscal Year's authoritative and projection state, for
    /// before/after diffing around a rejected operation (spec §7).
    /// </summary>
    public async Task<FiscalYearSnapshot> SnapshotAsync(
        long accountingBookId, long fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var entryCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM journal_entry WHERE fiscal_year_id = @FiscalYearId",
            new { FiscalYearId = fiscalYearId }, cancellationToken: cancellationToken));
        var lineCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*) FROM journal_entry_line l
            JOIN journal_entry e ON e.id = l.journal_entry_id
            WHERE e.fiscal_year_id = @FiscalYearId
            """,
            new { FiscalYearId = fiscalYearId }, cancellationToken: cancellationToken));
        var counters = await connection.QuerySingleAsync<FiscalYearCounters>(new CommandDefinition(
            """
            SELECT next_reference_number AS NextReferenceNumber,
                   next_journal_entry_number AS NextJournalEntryNumber,
                   finalized_through_date AS FinalizedThroughDate
            FROM fiscal_year WHERE id = @FiscalYearId
            """,
            new { FiscalYearId = fiscalYearId }, cancellationToken: cancellationToken));
        var turnoverChecksum = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            SELECT ISNULL(SUM(net_turnover), 0) FROM daily_account_turnover
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
            """,
            new { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId },
            cancellationToken: cancellationToken));
        var balanceChecksum = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            SELECT ISNULL(SUM(net_change), 0) FROM daily_account_balance
            WHERE accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId
            """,
            new { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId },
            cancellationToken: cancellationToken));

        return new FiscalYearSnapshot(
            entryCount, lineCount, counters.NextReferenceNumber, counters.NextJournalEntryNumber,
            turnoverChecksum, balanceChecksum);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(shardConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
