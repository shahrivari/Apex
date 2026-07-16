using Apex.Application.Abstractions.Data;
using Dapper;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories.Rows;

namespace Apex.Modules.Accounting.AccountingBooks.Repositories;

public sealed class AccountingBookWriteRepository : IAccountingBookWriteRepository
{
    private readonly IGeneralConnectionFactory _connectionFactory;

    public AccountingBookWriteRepository(IGeneralConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InsertAsync(
        AccountingBook book,
        CancellationToken cancellationToken = default)
    {
        await (await _connectionFactory.OpenAsync(cancellationToken)).ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO accounting_book (
                    id,
                    code,
                    title,
                    owner_type,
                    owner_id,
                    status,
                    created_at
                )
                VALUES (
                    @Id,
                    @Code,
                    @Title,
                    @OwnerType,
                    @OwnerId,
                    @Status,
                    @CreatedAt
                )
                """,
                new
                {
                    book.Id,
                    book.Code,
                    book.Title,
                    book.OwnerType,
                    book.OwnerId,
                    Status = book.Status.ToDatabaseValue(),
                    book.CreatedAt
                },
                transaction: _connectionFactory.Transaction,
                cancellationToken: cancellationToken));
    }

    public async Task<AccountingBook?> GetByIdForUpdateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var row = await (await _connectionFactory.OpenAsync(cancellationToken)).QuerySingleOrDefaultAsync<AccountingBookRow>(
            new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    code AS Code,
                    title AS Title,
                    owner_type AS OwnerType,
                    owner_id AS OwnerId,
                    status AS Status,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    activated_at AS ActivatedAt,
                    suspended_at AS SuspendedAt,
                    archived_at AS ArchivedAt
                FROM accounting_book WITH (UPDLOCK, ROWLOCK)
                WHERE id = @Id
                """,
                new { Id = id },
                transaction: _connectionFactory.Transaction,
                cancellationToken: cancellationToken));

        return row is null
            ? null
            : AccountingBook.CreateFromSql(
                row.Id,
                row.Code,
                row.Title,
                row.OwnerType,
                row.OwnerId,
                AccountingBookStatusExtensions.FromDatabaseValue(row.Status),
                row.CreatedAt,
                row.UpdatedAt,
                row.ActivatedAt,
                row.SuspendedAt,
                row.ArchivedAt);
    }

    public async Task<bool> ExistsByIdForUpdateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var count = await (await _connectionFactory.OpenAsync(cancellationToken)).ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM accounting_book WITH (UPDLOCK, HOLDLOCK) WHERE id = @Id",
                new { Id = id },
                transaction: _connectionFactory.Transaction,
                cancellationToken: cancellationToken));

        return count == 1;
    }

    public async Task<bool> ExistsByCodeForUpdateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var count = await (await _connectionFactory.OpenAsync(cancellationToken)).ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM accounting_book WITH (UPDLOCK, HOLDLOCK)
                WHERE code = @Code
                """,
                new { Code = code },
                transaction: _connectionFactory.Transaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> ExistsByOwnerForUpdateAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var count = await (await _connectionFactory.OpenAsync(cancellationToken)).ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM accounting_book WITH (UPDLOCK, HOLDLOCK)
                WHERE owner_type = @OwnerType AND owner_id = @OwnerId
                """,
                new { OwnerType = ownerType, OwnerId = ownerId },
                transaction: _connectionFactory.Transaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task UpdateStatusAsync(
        AccountingBook book,
        CancellationToken cancellationToken = default)
    {
        await (await _connectionFactory.OpenAsync(cancellationToken)).ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE accounting_book
                SET
                    status = @Status,
                    updated_at = @UpdatedAt,
                    activated_at = @ActivatedAt,
                    suspended_at = @SuspendedAt,
                    archived_at = @ArchivedAt
                WHERE id = @Id
                """,
                new
                {
                    book.Id,
                    Status = book.Status.ToDatabaseValue(),
                    book.UpdatedAt,
                    book.ActivatedAt,
                    book.SuspendedAt,
                    book.ArchivedAt
                },
                transaction: _connectionFactory.Transaction,
                cancellationToken: cancellationToken));
    }

}
