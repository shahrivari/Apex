using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public sealed class FiscalYearReadRepository(
    IShardConnectionFactory connectionFactory,
    IShardKeyFactory<long> shardKeyFactory) : IFiscalYearReadRepository
{
    internal const string Columns = """
        id AS Id, accounting_book_id AS AccountingBookId, title AS Title,
        start_date AS StartDate, end_date AS EndDate, status AS Status,
        finalized_through_date AS FinalizedThroughDate,
        next_reference_number AS NextReferenceNumber,
        next_journal_entry_number AS NextJournalEntryNumber,
        created_at AS CreatedAt, updated_at AS UpdatedAt, opened_at AS OpenedAt,
        closed_at AS ClosedAt, cancelled_at AS CancelledAt, cancellation_date AS CancellationDate
        """;

    public async Task<FiscalYearRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var shard = await connectionFactory.OpenAsync(
            shardKeyFactory.Create(id), cancellationToken: cancellationToken);
        return await shard.Connection.QuerySingleOrDefaultAsync<FiscalYearRow>(new CommandDefinition(
            $"SELECT {Columns} FROM fiscal_year WHERE id = @Id", new { Id = id },
            cancellationToken: cancellationToken));
    }
}
