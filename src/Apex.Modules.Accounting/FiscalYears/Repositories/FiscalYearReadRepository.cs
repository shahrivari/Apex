using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public sealed class FiscalYearReadRepository(IGeneralConnectionFactory connectionFactory)
    : IFiscalYearReadRepository
{
    private const string Columns = """
        id AS Id,
        accounting_book_id AS AccountingBookId,
        title AS Title,
        start_date AS StartDate,
        end_date AS EndDate,
        status AS Status,
        finalized_through_date AS FinalizedThroughDate,
        next_document_number AS NextDocumentNumber,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt,
        opened_at AS OpenedAt,
        closed_at AS ClosedAt,
        cancelled_at AS CancelledAt,
        cancellation_date AS CancellationDate
        """;

    public async Task<FiscalYearRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<FiscalYearRow>(new CommandDefinition(
            $"SELECT {Columns} FROM fiscal_year WHERE id = @Id",
            new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<(IReadOnlyList<FiscalYearRow> Items, int TotalCount)> ListAsync(
        long? accountingBookId, string? status, DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();
        if (accountingBookId.HasValue)
        {
            where.Add("accounting_book_id = @AccountingBookId");
            parameters.Add("AccountingBookId", accountingBookId.Value);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            where.Add("status = @Status");
            parameters.Add("Status", status);
        }
        if (fromDate.HasValue)
        {
            where.Add("end_date >= @FromDate");
            parameters.Add("FromDate", fromDate.Value);
        }
        if (toDate.HasValue)
        {
            where.Add("start_date <= @ToDate");
            parameters.Add("ToDate", toDate.Value);
        }

        var whereClause = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        var skip = (page - 1) * pageSize;
        parameters.Add("Skip", skip);
        parameters.Add("PageSize", pageSize);
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM fiscal_year {whereClause}", parameters,
            cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<FiscalYearRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM fiscal_year
            {whereClause}
            ORDER BY start_date DESC, id DESC
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY
            """, parameters, cancellationToken: cancellationToken))).AsList();
        return (items, count);
    }

    public async Task<FiscalYearRow?> ResolveForDateAsync(
        long accountingBookId, DateOnly accountingDate, string? requiredStatus,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<FiscalYearRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM fiscal_year
            WHERE accounting_book_id = @AccountingBookId
              AND start_date <= @AccountingDate
              AND ISNULL(cancellation_date, end_date) >= @AccountingDate
              AND (@RequiredStatus IS NULL OR status = @RequiredStatus)
            """,
            new { AccountingBookId = accountingBookId, AccountingDate = accountingDate, RequiredStatus = requiredStatus },
            cancellationToken: cancellationToken))).AsList();
        return rows.Count switch
        {
            0 => null,
            1 => rows[0],
            _ => throw new InvalidOperationException("More than one fiscal year contains the accounting date.")
        };
    }
}
