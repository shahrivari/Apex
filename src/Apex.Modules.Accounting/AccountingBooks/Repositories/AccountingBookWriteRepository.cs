using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Dapper;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.SqlModels;

namespace Apex.Modules.Accounting.AccountingBooks.Repositories;

public sealed class AccountingBookWriteRepository
{
    private readonly IWriteDbSession _session;

    public AccountingBookWriteRepository(IWriteDbSession session)
    {
        _session = session;
    }

    public async Task InsertAsync(
        AccountingBook book,
        CancellationToken cancellationToken = default)
    {
        await _session.Connection.ExecuteAsync(
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
                transaction: _session.Transaction,
                cancellationToken: cancellationToken));
    }

    public async Task<AccountingBook?> GetByIdForUpdateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var model = await _session.Connection.QuerySingleOrDefaultAsync<AccountingBookSqlModel>(
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
                transaction: _session.Transaction,
                cancellationToken: cancellationToken));

        return model == null ? null : model.MapToDomain();
    }

    public async Task<bool> ExistsByCodeForUpdateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var count = await _session.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM accounting_book
                WHERE code = @Code
                """,
                new { Code = code },
                transaction: _session.Transaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> ExistsByOwnerForUpdateAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var count = await _session.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM accounting_book
                WHERE owner_type = @OwnerType AND owner_id = @OwnerId
                """,
                new { OwnerType = ownerType, OwnerId = ownerId },
                transaction: _session.Transaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task UpdateStatusAsync(
        AccountingBook book,
        CancellationToken cancellationToken = default)
    {
        await _session.Connection.ExecuteAsync(
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
                transaction: _session.Transaction,
                cancellationToken: cancellationToken));
    }

}
