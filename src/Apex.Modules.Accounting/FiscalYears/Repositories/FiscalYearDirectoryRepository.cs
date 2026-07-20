using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public sealed class FiscalYearDirectoryRepository(IGeneralConnectionFactory connectionFactory)
    : IFiscalYearDirectoryRepository
{
    private const string Columns = """
        id AS Id, accounting_book_id AS AccountingBookId, title AS Title,
        start_date AS StartDate, end_date AS EndDate, status AS Status,
        finalized_through_date AS FinalizedThroughDate,
        CAST(1 AS BIGINT) AS NextReferenceNumber,
        CAST(1 AS BIGINT) AS NextJournalEntryNumber,
        created_at AS CreatedAt, updated_at AS UpdatedAt, opened_at AS OpenedAt,
        closed_at AS ClosedAt, cancelled_at AS CancelledAt, cancellation_date AS CancellationDate
        """;

    public async Task<(IReadOnlyList<FiscalYearRow> Items, int TotalCount)> ListAsync(
        long? accountingBookId, string? status, DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();
        if (accountingBookId.HasValue)
        { where.Add("accounting_book_id = @AccountingBookId"); parameters.Add("AccountingBookId", accountingBookId); }
        if (!string.IsNullOrWhiteSpace(status))
        { where.Add("status = @Status"); parameters.Add("Status", status); }
        if (fromDate.HasValue)
        { where.Add("ISNULL(cancellation_date, end_date) >= @FromDate"); parameters.Add("FromDate", fromDate); }
        if (toDate.HasValue)
        { where.Add("start_date <= @ToDate"); parameters.Add("ToDate", toDate); }
        var clause = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM fiscal_year_directory {clause}", parameters,
            cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<FiscalYearRow>(new CommandDefinition(
            $"SELECT {Columns} FROM fiscal_year_directory {clause} ORDER BY start_date DESC, id DESC OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters, cancellationToken: cancellationToken))).AsList();
        return (rows, count);
    }

    public async Task<FiscalYearRow?> ResolveForDateAsync(long accountingBookId, DateOnly accountingDate,
        string? requiredStatus, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<FiscalYearRow>(new CommandDefinition(
            $"SELECT {Columns} FROM fiscal_year_directory WHERE accounting_book_id = @AccountingBookId AND start_date <= @AccountingDate AND ISNULL(cancellation_date, end_date) >= @AccountingDate",
            new { AccountingBookId = accountingBookId, AccountingDate = accountingDate },
            cancellationToken: cancellationToken))).AsList();
        var row = rows.Count switch { 0 => null, 1 => rows[0], _ => throw new InvalidOperationException("More than one fiscal year contains the accounting date.") };
        return row is not null && (requiredStatus is null || row.Status == requiredStatus) ? row : null;
    }

    public async Task<bool> HasOverlapAsync(long accountingBookId, DateOnly startDate, DateOnly endDate,
        long? excludedId = null, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM fiscal_year_directory WHERE accounting_book_id = @AccountingBookId AND id <> ISNULL(@ExcludedId, -1) AND start_date <= @EndDate AND ISNULL(cancellation_date, end_date) >= @StartDate",
            new { AccountingBookId = accountingBookId, StartDate = startDate, EndDate = endDate, ExcludedId = excludedId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> HasOtherOpenAsync(long accountingBookId, long excludedId,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM fiscal_year_directory WHERE accounting_book_id = @AccountingBookId AND status = 'OPEN' AND id <> @ExcludedId",
            new { AccountingBookId = accountingBookId, ExcludedId = excludedId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task UpsertAsync(FiscalYear fiscalYear, DateTime syncedAt,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE fiscal_year_directory WITH (HOLDLOCK) AS target
            USING (VALUES (@Id)) AS source (id) ON target.id = source.id
            WHEN MATCHED THEN UPDATE SET accounting_book_id=@AccountingBookId, title=@Title,
                start_date=@StartDate, end_date=@EndDate, status=@Status,
                finalized_through_date=@FinalizedThroughDate, updated_at=@UpdatedAt,
                opened_at=@OpenedAt, closed_at=@ClosedAt, cancelled_at=@CancelledAt,
                cancellation_date=@CancellationDate, directory_synced_at=@SyncedAt
            WHEN NOT MATCHED THEN INSERT (id, accounting_book_id, title, start_date, end_date,
                status, finalized_through_date, created_at, updated_at, opened_at, closed_at,
                cancelled_at, cancellation_date, directory_synced_at)
            VALUES (@Id,@AccountingBookId,@Title,@StartDate,@EndDate,@Status,@FinalizedThroughDate,
                @CreatedAt,@UpdatedAt,@OpenedAt,@ClosedAt,@CancelledAt,@CancellationDate,@SyncedAt);
            """, new
            {
                fiscalYear.Id, fiscalYear.AccountingBookId, fiscalYear.Title, fiscalYear.StartDate,
                fiscalYear.EndDate, Status = fiscalYear.Status.ToDatabaseValue(), fiscalYear.FinalizedThroughDate,
                fiscalYear.CreatedAt, fiscalYear.UpdatedAt, fiscalYear.OpenedAt, fiscalYear.ClosedAt,
                fiscalYear.CancelledAt, fiscalYear.CancellationDate, SyncedAt = syncedAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM fiscal_year_directory WHERE id = @Id", new { Id = fiscalYearId },
            cancellationToken: cancellationToken));
    }
}
