using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public sealed class FiscalYearWriteRepository(IGeneralConnectionFactory connectionFactory)
    : IFiscalYearWriteRepository
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

    public async Task InsertAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO fiscal_year (
                id, accounting_book_id, title, start_date, end_date, status,
                finalized_through_date, next_document_number, created_at)
            VALUES (
                @Id, @AccountingBookId, @Title, @StartDate, @EndDate, @Status,
                @FinalizedThroughDate, @NextDocumentNumber, @CreatedAt)
            """,
            new
            {
                fiscalYear.Id, fiscalYear.AccountingBookId, fiscalYear.Title,
                fiscalYear.StartDate, fiscalYear.EndDate,
                Status = fiscalYear.Status.ToDatabaseValue(), fiscalYear.FinalizedThroughDate,
                fiscalYear.NextDocumentNumber, fiscalYear.CreatedAt
            }, connectionFactory.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task<FiscalYear?> GetByIdForUpdateAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<FiscalYearRow>(new CommandDefinition(
            $"SELECT {Columns} FROM fiscal_year WITH (UPDLOCK, ROWLOCK) WHERE id = @Id",
            new { Id = id }, connectionFactory.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<bool> HasOverlapForUpdateAsync(
        long accountingBookId, DateOnly startDate, DateOnly endDate, long? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM fiscal_year WITH (UPDLOCK, HOLDLOCK, INDEX(ix_fiscal_year_book_dates))
            WHERE accounting_book_id = @AccountingBookId
              AND id <> ISNULL(@ExcludedId, -1)
              AND start_date <= @EndDate
              AND ISNULL(cancellation_date, end_date) >= @StartDate
            """,
            new { AccountingBookId = accountingBookId, StartDate = startDate, EndDate = endDate, ExcludedId = excludedId },
            connectionFactory.Transaction, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> HasOtherOpenForUpdateAsync(
        long accountingBookId, long excludedId, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM fiscal_year WITH (UPDLOCK, HOLDLOCK)
            WHERE accounting_book_id = @AccountingBookId AND status = 'OPEN' AND id <> @ExcludedId
            """, new { AccountingBookId = accountingBookId, ExcludedId = excludedId },
            connectionFactory.Transaction, cancellationToken: cancellationToken)) > 0;
    }

    public async Task UpdateAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE fiscal_year SET
                title = @Title,
                start_date = @StartDate,
                end_date = @EndDate,
                status = @Status,
                finalized_through_date = @FinalizedThroughDate,
                updated_at = @UpdatedAt,
                opened_at = @OpenedAt,
                closed_at = @ClosedAt,
                cancelled_at = @CancelledAt,
                cancellation_date = @CancellationDate
            WHERE id = @Id
            """,
            new
            {
                fiscalYear.Id, fiscalYear.Title, fiscalYear.StartDate, fiscalYear.EndDate,
                Status = fiscalYear.Status.ToDatabaseValue(), fiscalYear.FinalizedThroughDate,
                fiscalYear.UpdatedAt, fiscalYear.OpenedAt, fiscalYear.ClosedAt,
                fiscalYear.CancelledAt, fiscalYear.CancellationDate
            }, connectionFactory.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM fiscal_year WHERE id = @Id AND status = 'DRAFT'", new { Id = id },
            connectionFactory.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task<long?> AllocateDocumentNumberAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            """
            UPDATE fiscal_year
            SET next_document_number = next_document_number + 1
            OUTPUT deleted.next_document_number
            WHERE id = @Id AND status = 'OPEN' AND next_document_number < 9223372036854775807
            """, new { Id = id }, connectionFactory.Transaction, cancellationToken: cancellationToken));
    }

    private static FiscalYear Map(FiscalYearRow row) => FiscalYear.Rehydrate(
        row.Id, row.AccountingBookId, row.Title, row.StartDate, row.EndDate,
        FiscalYearStatusExtensions.FromDatabaseValue(row.Status), row.FinalizedThroughDate,
        row.NextDocumentNumber, row.CreatedAt, row.UpdatedAt, row.OpenedAt, row.ClosedAt,
        row.CancelledAt, row.CancellationDate);

    private static void EnsureOneRow(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException($"Expected one fiscal year row to be affected, but affected {affected}.");
    }
}
