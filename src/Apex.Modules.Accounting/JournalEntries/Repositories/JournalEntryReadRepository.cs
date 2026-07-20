using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryReadRepository(
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory) : IJournalEntryReadRepository
{
    internal const string HeaderColumns = """
        id AS Id,
        accounting_book_id AS AccountingBookId,
        fiscal_year_id AS FiscalYearId,
        reference_number AS ReferenceNumber,
        journal_entry_number AS JournalEntryNumber,
        number_finalized AS NumberFinalized,
        accounting_date AS AccountingDate,
        registered_at AS RegisteredAt,
        description AS Description,
        document_type AS DocumentType,
        insertion_type AS InsertionType,
        status AS Status,
        balance_effect AS BalanceEffect,
        source_type AS SourceType,
        source_reference AS SourceReference,
        reversal_of_reference_number AS ReversalOfReferenceNumber,
        reversed_by_reference_number AS ReversedByReferenceNumber,
        reversal_reason AS ReversalReason,
        posted_at AS PostedAt,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt
        """;

    internal const string LineColumns = """
        id AS Id,
        journal_entry_id AS JournalEntryId,
        row_number AS RowNumber,
        account_class_code AS AccountClassCode,
        general_account_code AS GeneralAccountCode,
        subsidiary_account_code AS SubsidiaryAccountCode,
        detail_account_code AS DetailAccountCode,
        side AS Side,
        amount AS Amount,
        description AS Description
        """;

    public Task<JournalEntryWithLines?> GetByIdAsync(
        long fiscalYearId, long id, CancellationToken cancellationToken = default) =>
        GetSingleAsync(
            fiscalYearId,
            "fiscal_year_id = @FiscalYearId AND id = @Id",
            new { FiscalYearId = fiscalYearId, Id = id }, cancellationToken);

    public Task<JournalEntryWithLines?> GetByReferenceNumberAsync(
        long accountingBookId, long fiscalYearId, long referenceNumber,
        CancellationToken cancellationToken = default) =>
        GetSingleAsync(
            fiscalYearId,
            "accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId AND reference_number = @ReferenceNumber",
            new { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, ReferenceNumber = referenceNumber },
            cancellationToken);

    public Task<JournalEntryWithLines?> GetByJournalEntryNumberAsync(
        long accountingBookId, long fiscalYearId, long journalEntryNumber,
        CancellationToken cancellationToken = default) =>
        GetSingleAsync(
            fiscalYearId,
            "accounting_book_id = @AccountingBookId AND fiscal_year_id = @FiscalYearId AND journal_entry_number = @JournalEntryNumber",
            new { AccountingBookId = accountingBookId, FiscalYearId = fiscalYearId, JournalEntryNumber = journalEntryNumber },
            cancellationToken);

    public Task<JournalEntryWithLines?> GetBySourceReferenceAsync(
        long fiscalYearId, string? sourceType, string sourceReference,
        CancellationToken cancellationToken = default) =>
        GetSingleAsync(
            fiscalYearId,
            "fiscal_year_id = @FiscalYearId AND source_reference = @SourceReference AND source_type = @SourceType",
            new { FiscalYearId = fiscalYearId, SourceType = sourceType, SourceReference = sourceReference },
            cancellationToken);

    public async Task<(IReadOnlyList<JournalEntryRow> Items, int TotalCount)> SearchAsync(
        JournalEntrySearchFilter filter, CancellationToken cancellationToken = default)
    {
        var where = new List<string> { "fiscal_year_id = @FiscalYearId" };
        var parameters = new DynamicParameters();
        parameters.Add("FiscalYearId", filter.FiscalYearId);

        void Add(bool condition, string clause, string name, object? value)
        {
            if (!condition)
                return;
            where.Add(clause);
            parameters.Add(name, value);
        }

        Add(filter.AccountingBookId.HasValue, "accounting_book_id = @AccountingBookId", "AccountingBookId", filter.AccountingBookId);
        Add(filter.FromDate.HasValue, "accounting_date >= @FromDate", "FromDate", filter.FromDate);
        Add(filter.ToDate.HasValue, "accounting_date <= @ToDate", "ToDate", filter.ToDate);
        Add(filter.ReferenceNumber.HasValue, "reference_number = @ReferenceNumber", "ReferenceNumber", filter.ReferenceNumber);
        Add(filter.JournalEntryNumber.HasValue, "journal_entry_number = @JournalEntryNumber", "JournalEntryNumber", filter.JournalEntryNumber);
        Add(!string.IsNullOrWhiteSpace(filter.Status), "status = @Status", "Status", filter.Status);
        Add(!string.IsNullOrWhiteSpace(filter.BalanceEffect), "balance_effect = @BalanceEffect", "BalanceEffect", filter.BalanceEffect);
        Add(!string.IsNullOrWhiteSpace(filter.DocumentType), "document_type = @DocumentType", "DocumentType", filter.DocumentType);
        Add(!string.IsNullOrWhiteSpace(filter.InsertionType), "insertion_type = @InsertionType", "InsertionType", filter.InsertionType);
        Add(!string.IsNullOrWhiteSpace(filter.SourceType), "source_type = @SourceType", "SourceType", filter.SourceType);
        Add(!string.IsNullOrWhiteSpace(filter.SourceReference), "source_reference = @SourceReference", "SourceReference", filter.SourceReference);

        var lineConditions = new List<string>();
        void AddLine(bool condition, string clause, string name, object? value)
        {
            if (!condition)
                return;
            lineConditions.Add(clause);
            parameters.Add(name, value);
        }

        AddLine(!string.IsNullOrWhiteSpace(filter.AccountClassCode), "l.account_class_code = @AccountClassCode", "AccountClassCode", filter.AccountClassCode?.Trim());
        AddLine(!string.IsNullOrWhiteSpace(filter.GeneralAccountCode), "l.general_account_code = @GeneralAccountCode", "GeneralAccountCode", filter.GeneralAccountCode?.Trim());
        AddLine(!string.IsNullOrWhiteSpace(filter.SubsidiaryAccountCode), "l.subsidiary_account_code = @SubsidiaryAccountCode", "SubsidiaryAccountCode", filter.SubsidiaryAccountCode?.Trim());
        AddLine(!string.IsNullOrWhiteSpace(filter.DetailAccountCode), "l.detail_account_code = @DetailAccountCode", "DetailAccountCode", filter.DetailAccountCode?.Trim());

        if (lineConditions.Count > 0)
            where.Add(
                $"""
                EXISTS (
                    SELECT 1 FROM journal_entry_line l
                    WHERE l.journal_entry_id = journal_entry.id AND {string.Join(" AND ", lineConditions)})
                """);

        var whereClause = "WHERE " + string.Join(" AND ", where);
        var skip = (filter.Page - 1) * filter.PageSize;
        parameters.Add("Skip", skip);
        parameters.Add("PageSize", filter.PageSize);

        await using var shard = await OpenAsync(filter.FiscalYearId, cancellationToken);
        var count = await shard.Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM journal_entry {whereClause}", parameters,
            transaction: shard.Transaction, cancellationToken: cancellationToken));
        var items = (await shard.Connection.QueryAsync<JournalEntryRow>(new CommandDefinition(
            $"""
            SELECT {HeaderColumns}
            FROM journal_entry
            {whereClause}
            ORDER BY accounting_date DESC, reference_number DESC
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY
            """, parameters, transaction: shard.Transaction, cancellationToken: cancellationToken))).AsList();
        return (items, count);
    }

    private async Task<JournalEntryWithLines?> GetSingleAsync(
        long fiscalYearId, string predicate, object parameters, CancellationToken cancellationToken)
    {
        await using var shard = await OpenAsync(fiscalYearId, cancellationToken);
        var header = await shard.Connection.QuerySingleOrDefaultAsync<JournalEntryRow>(new CommandDefinition(
            $"SELECT {HeaderColumns} FROM journal_entry WHERE {predicate}",
            parameters, transaction: shard.Transaction, cancellationToken: cancellationToken));
        if (header is null)
            return null;

        var lines = (await shard.Connection.QueryAsync<JournalEntryLineRow>(new CommandDefinition(
            $"SELECT {LineColumns} FROM journal_entry_line WHERE journal_entry_id = @EntryId ORDER BY row_number",
            new { EntryId = header.Id }, transaction: shard.Transaction, cancellationToken: cancellationToken))).AsList();
        return new JournalEntryWithLines(header, lines);
    }

    private Task<IShardConnection> OpenAsync(long fiscalYearId, CancellationToken cancellationToken) =>
        shardConnectionFactory.OpenAsync(shardKeyFactory.Create(fiscalYearId), beginTransaction: false, cancellationToken);
}
