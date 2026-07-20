using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.JournalEntries.Repositories;

public sealed class JournalEntryWriteRepository : IJournalEntryWriteRepository
{
    public async Task InsertAsync(
        IShardConnection connection, JournalEntry entry, CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO journal_entry (
                id, accounting_book_id, fiscal_year_id, reference_number, journal_entry_number,
                number_finalized, accounting_date, registered_at, description, document_type,
                insertion_type, status, balance_effect, source_type, source_reference,
                reversal_of_reference_number, reversed_by_reference_number, reversal_reason,
                posted_at, created_at, updated_at)
            VALUES (
                @Id, @AccountingBookId, @FiscalYearId, @ReferenceNumber, @JournalEntryNumber,
                @NumberFinalized, @AccountingDate, @RegisteredAt, @Description, @DocumentType,
                @InsertionType, @Status, @BalanceEffect, @SourceType, @SourceReference,
                @ReversalOfReferenceNumber, @ReversedByReferenceNumber, @ReversalReason,
                @PostedAt, @CreatedAt, @UpdatedAt)
            """,
            HeaderParameters(entry), connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);

        await InsertLinesAsync(connection, entry, cancellationToken);
    }

    public async Task<JournalEntry?> GetForUpdateAsync(
        IShardConnection connection, long fiscalYearId, long id,
        CancellationToken cancellationToken = default)
    {
        var header = await connection.Connection.QuerySingleOrDefaultAsync<JournalEntryRow>(new CommandDefinition(
            $"SELECT {JournalEntryReadRepository.HeaderColumns} FROM journal_entry WITH (UPDLOCK, ROWLOCK) WHERE fiscal_year_id = @FiscalYearId AND id = @Id",
            new { FiscalYearId = fiscalYearId, Id = id }, connection.Transaction,
            cancellationToken: cancellationToken));
        if (header is null)
            return null;

        var lines = (await connection.Connection.QueryAsync<JournalEntryLineRow>(new CommandDefinition(
            $"SELECT {JournalEntryReadRepository.LineColumns} FROM journal_entry_line WHERE journal_entry_id = @EntryId ORDER BY row_number",
            new { EntryId = id }, connection.Transaction, cancellationToken: cancellationToken))).AsList();

        return Map(header, lines);
    }

    public async Task<JournalEntry?> GetBySourceReferenceForUpdateAsync(
        IShardConnection connection, long fiscalYearId, string sourceType, string sourceReference,
        CancellationToken cancellationToken = default)
    {
        var header = await connection.Connection.QuerySingleOrDefaultAsync<JournalEntryRow>(new CommandDefinition(
            $"""
            SELECT {JournalEntryReadRepository.HeaderColumns}
            FROM journal_entry WITH (UPDLOCK, HOLDLOCK, INDEX(ux_journal_entry_source))
            WHERE fiscal_year_id = @FiscalYearId
              AND source_type = @SourceType
              AND source_reference = @SourceReference
            """,
            new { FiscalYearId = fiscalYearId, SourceType = sourceType, SourceReference = sourceReference },
            connection.Transaction, cancellationToken: cancellationToken));
        if (header is null)
            return null;

        var lines = (await connection.Connection.QueryAsync<JournalEntryLineRow>(new CommandDefinition(
            $"SELECT {JournalEntryReadRepository.LineColumns} FROM journal_entry_line WHERE journal_entry_id = @EntryId ORDER BY row_number",
            new { EntryId = header.Id }, connection.Transaction,
            cancellationToken: cancellationToken))).AsList();
        return Map(header, lines);
    }

    public async Task<JournalEntry?> GetByReferenceNumberForUpdateAsync(
        IShardConnection connection, long fiscalYearId, long referenceNumber,
        CancellationToken cancellationToken = default)
    {
        var header = await connection.Connection.QuerySingleOrDefaultAsync<JournalEntryRow>(new CommandDefinition(
            $"SELECT {JournalEntryReadRepository.HeaderColumns} FROM journal_entry WITH (UPDLOCK, ROWLOCK) WHERE fiscal_year_id = @FiscalYearId AND reference_number = @ReferenceNumber",
            new { FiscalYearId = fiscalYearId, ReferenceNumber = referenceNumber },
            connection.Transaction, cancellationToken: cancellationToken));
        if (header is null)
            return null;

        var lines = (await connection.Connection.QueryAsync<JournalEntryLineRow>(new CommandDefinition(
            $"SELECT {JournalEntryReadRepository.LineColumns} FROM journal_entry_line WHERE journal_entry_id = @EntryId ORDER BY row_number",
            new { EntryId = header.Id }, connection.Transaction,
            cancellationToken: cancellationToken))).AsList();
        return Map(header, lines);
    }

    public async Task LinkReversalAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry original,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE journal_entry SET reversed_by_reference_number = @ReversedByReferenceNumber, updated_at = @UpdatedAt WHERE fiscal_year_id = @FiscalYearId AND id = @Id AND reversed_by_reference_number IS NULL AND status = 'POSTED'",
            new
            {
                original.Id,
                FiscalYearId = fiscalYearId,
                original.ReversedByReferenceNumber,
                original.UpdatedAt
            }, connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task MarkPostedAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry entry,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE journal_entry SET
                status = @Status,
                posted_at = @PostedAt,
                updated_at = @UpdatedAt
            WHERE fiscal_year_id = @FiscalYearId AND id = @Id AND status = 'DRAFT'
            """,
            new
            {
                entry.Id,
                FiscalYearId = fiscalYearId,
                Status = entry.Status.ToDatabaseValue(),
                entry.PostedAt,
                entry.UpdatedAt
            }, connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task UpdateHeaderAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry entry,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE journal_entry SET
                journal_entry_number = @JournalEntryNumber,
                number_finalized = @NumberFinalized,
                accounting_date = @AccountingDate,
                description = @Description,
                document_type = @DocumentType,
                balance_effect = @BalanceEffect,
                updated_at = @UpdatedAt
            WHERE fiscal_year_id = @FiscalYearId AND id = @Id
            """,
            HeaderParameters(entry), connection.Transaction, cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task ReplaceLinesAsync(
        IShardConnection connection, long fiscalYearId, JournalEntry entry,
        CancellationToken cancellationToken = default)
    {
        await connection.Connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE line
            FROM journal_entry_line line
            INNER JOIN journal_entry entry ON entry.id = line.journal_entry_id
            WHERE entry.fiscal_year_id = @FiscalYearId AND entry.id = @EntryId
            """,
            new { FiscalYearId = fiscalYearId, EntryId = entry.Id }, connection.Transaction,
            cancellationToken: cancellationToken));
        await InsertLinesAsync(connection, entry, cancellationToken);

        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE journal_entry SET updated_at = @UpdatedAt WHERE fiscal_year_id = @FiscalYearId AND id = @Id",
            new { FiscalYearId = fiscalYearId, entry.Id, entry.UpdatedAt }, connection.Transaction,
            cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    public async Task DeleteAsync(
        IShardConnection connection, long fiscalYearId, long id,
        CancellationToken cancellationToken = default)
    {
        var affected = await connection.Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM journal_entry WHERE fiscal_year_id = @FiscalYearId AND id = @Id AND status = 'DRAFT'",
            new { FiscalYearId = fiscalYearId, Id = id }, connection.Transaction,
            cancellationToken: cancellationToken));
        EnsureOneRow(affected);
    }

    private static async Task InsertLinesAsync(
        IShardConnection connection, JournalEntry entry, CancellationToken cancellationToken)
    {
        foreach (var line in entry.Lines)
        {
            await connection.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO journal_entry_line (
                    id, journal_entry_id, row_number, account_class_code, general_account_code,
                    subsidiary_account_code, detail_account_code, side, amount, description)
                VALUES (
                    @Id, @JournalEntryId, @RowNumber, @AccountClassCode, @GeneralAccountCode,
                    @SubsidiaryAccountCode, @DetailAccountCode, @Side, @Amount, @Description)
                """,
                new
                {
                    line.Id,
                    JournalEntryId = entry.Id,
                    line.RowNumber,
                    line.AccountClassCode,
                    line.GeneralAccountCode,
                    line.SubsidiaryAccountCode,
                    line.DetailAccountCode,
                    Side = line.Side.ToDatabaseValue(),
                    line.Amount,
                    line.Description
                }, connection.Transaction, cancellationToken: cancellationToken));
        }
    }

    private static object HeaderParameters(JournalEntry entry) => new
    {
        entry.Id,
        entry.AccountingBookId,
        entry.FiscalYearId,
        entry.ReferenceNumber,
        entry.JournalEntryNumber,
        entry.NumberFinalized,
        entry.AccountingDate,
        entry.RegisteredAt,
        entry.Description,
        DocumentType = entry.DocumentType.ToDatabaseValue(),
        InsertionType = entry.InsertionType.ToDatabaseValue(),
        Status = entry.Status.ToDatabaseValue(),
        BalanceEffect = entry.BalanceEffect.ToDatabaseValue(),
        entry.SourceType,
        entry.SourceReference,
        entry.ReversalOfReferenceNumber,
        entry.ReversedByReferenceNumber,
        entry.ReversalReason,
        entry.PostedAt,
        entry.CreatedAt,
        entry.UpdatedAt
    };

    private static JournalEntry Map(JournalEntryRow header, IReadOnlyList<JournalEntryLineRow> lines)
    {
        var mappedLines = lines
            .Select(line => JournalEntryLine.Rehydrate(
                line.Id, line.RowNumber, JournalEntrySideExtensions.FromDatabaseValue(line.Side), line.Amount,
                line.AccountClassCode, line.GeneralAccountCode, line.SubsidiaryAccountCode,
                line.DetailAccountCode, line.Description))
            .ToList();

        return JournalEntry.Rehydrate(
            header.Id, header.AccountingBookId, header.FiscalYearId, header.ReferenceNumber,
            header.JournalEntryNumber, header.NumberFinalized, header.AccountingDate, header.RegisteredAt,
            header.Description, DocumentTypeExtensions.FromDatabaseValue(header.DocumentType),
            InsertionTypeExtensions.FromDatabaseValue(header.InsertionType),
            JournalEntryStatusExtensions.FromDatabaseValue(header.Status),
            BalanceEffectExtensions.FromDatabaseValue(header.BalanceEffect),
            header.SourceType, header.SourceReference, header.ReversalOfReferenceNumber,
            header.ReversedByReferenceNumber, header.ReversalReason, header.PostedAt,
            header.CreatedAt, header.UpdatedAt, mappedLines);
    }

    private static void EnsureOneRow(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException(
                $"Expected one journal entry row to be affected, but affected {affected}.");
    }
}
