using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public sealed class FiscalYearWriteRepository : IFiscalYearWriteRepository
{
    public async Task InsertAsync(IShardConnection connection, FiscalYear fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO fiscal_year (
                id, accounting_book_id, title, start_date, end_date, status,
                finalized_through_date, next_reference_number, next_journal_entry_number, created_at)
            VALUES (@Id, @AccountingBookId, @Title, @StartDate, @EndDate, @Status,
                @FinalizedThroughDate, @NextReferenceNumber, @NextJournalEntryNumber, @CreatedAt)
            """, Parameters(fiscalYear), connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task<FiscalYear?> GetByIdForUpdateAsync(IShardConnection connection, long id,
        CancellationToken cancellationToken = default)
    {
        var row = await connection.Connection.QuerySingleOrDefaultAsync<FiscalYearRow>(new CommandDefinition(
            $"SELECT {FiscalYearReadRepository.Columns} FROM fiscal_year WITH (UPDLOCK, ROWLOCK) WHERE id = @Id",
            new { Id = id }, connection.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task UpdateAsync(IShardConnection connection, FiscalYear fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE fiscal_year SET title = @Title, start_date = @StartDate, end_date = @EndDate,
                status = @Status, finalized_through_date = @FinalizedThroughDate,
                next_reference_number = @NextReferenceNumber,
                next_journal_entry_number = @NextJournalEntryNumber,
                updated_at = @UpdatedAt, opened_at = @OpenedAt, closed_at = @ClosedAt,
                cancelled_at = @CancelledAt, cancellation_date = @CancellationDate
            WHERE id = @Id
            """, Parameters(fiscalYear), connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task DeleteAsync(IShardConnection connection, long id,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM fiscal_year WHERE id = @Id AND status = 'DRAFT'", new { Id = id },
            connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    internal static FiscalYear Map(FiscalYearRow row) => FiscalYear.Rehydrate(
        row.Id, row.AccountingBookId, row.Title, row.StartDate, row.EndDate,
        FiscalYearStatusExtensions.FromDatabaseValue(row.Status), row.FinalizedThroughDate,
        row.NextReferenceNumber, row.NextJournalEntryNumber, row.CreatedAt, row.UpdatedAt,
        row.OpenedAt, row.ClosedAt, row.CancelledAt, row.CancellationDate);

    private static object Parameters(FiscalYear fiscalYear) => new
    {
        fiscalYear.Id, fiscalYear.AccountingBookId, fiscalYear.Title, fiscalYear.StartDate,
        fiscalYear.EndDate, Status = fiscalYear.Status.ToDatabaseValue(), fiscalYear.FinalizedThroughDate,
        fiscalYear.NextReferenceNumber, fiscalYear.NextJournalEntryNumber, fiscalYear.CreatedAt,
        fiscalYear.UpdatedAt, fiscalYear.OpenedAt, fiscalYear.ClosedAt, fiscalYear.CancelledAt,
        fiscalYear.CancellationDate
    };

    private static void EnsureOneRow(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException($"Expected one fiscal year row, but affected {affected}.");
    }
}
