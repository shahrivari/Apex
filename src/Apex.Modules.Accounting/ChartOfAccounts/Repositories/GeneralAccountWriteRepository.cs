using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class GeneralAccountWriteRepository(IGeneralConnectionFactory factory) : IGeneralAccountWriteRepository
{
    private const string Columns = """
        id Id,
        account_class_id AccountClassId,
        code Code,
        name Name,
        nature Nature,
        status Status,
        created_at CreatedAt,
        updated_at UpdatedAt,
        archived_at ArchivedAt
        """;

    public async Task<GeneralAccount?> GetForUpdateAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<GeneralAccountRow>(new CommandDefinition(
            $"SELECT {Columns} FROM general_account WITH (UPDLOCK, ROWLOCK) WHERE id = @Id",
            new { Id = id }, factory.Transaction, cancellationToken: ct));
        return row is null
            ? null
            : GeneralAccount.Rehydrate(row.Id, row.AccountClassId, row.Code, row.Name,
                row.Nature.ToAccountNature(), row.Status.ToAccountStatus(),
                row.CreatedAt, row.UpdatedAt, row.ArchivedAt);
    }

    public async Task<bool> CodeExistsAsync(
        long parentId, string code, long? excludingId = null, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM general_account WITH (UPDLOCK, HOLDLOCK)
            WHERE account_class_id = @ParentId AND code = @Code AND (@ExcludingId IS NULL OR id <> @ExcludingId)
            """, new { ParentId = parentId, Code = code, ExcludingId = excludingId },
            factory.Transaction, cancellationToken: ct));
        return count > 0;
    }

    public async Task<bool> HasActiveChildrenAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM subsidiary_account WITH (UPDLOCK, HOLDLOCK)
            WHERE general_account_id = @Id AND status = 'ACTIVE'
            """, new { Id = id }, factory.Transaction, cancellationToken: ct));
        return count > 0;
    }

    public async Task InsertAsync(GeneralAccount value, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO general_account(id, account_class_id, code, name, nature, status, created_at)
            VALUES(@Id, @AccountClassId, @Code, @Name, @Nature, @Status, @CreatedAt)
            """,
            new
            {
                value.Id, value.AccountClassId, value.Code, value.Name,
                Nature = value.Nature.ToDatabaseValue(), Status = value.Status.ToDatabaseValue(), value.CreatedAt
            }, factory.Transaction, cancellationToken: ct));
        EnsureOneRow(affected);
    }

    public async Task UpdateAsync(GeneralAccount value, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE general_account SET name = @Name, status = @Status, updated_at = @UpdatedAt, archived_at = @ArchivedAt WHERE id = @Id",
            new { value.Id, value.Name, Status = value.Status.ToDatabaseValue(), value.UpdatedAt, value.ArchivedAt },
            factory.Transaction, cancellationToken: ct));
        EnsureOneRow(affected);
    }

    private static void EnsureOneRow(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException($"Expected one general account row to be affected, but affected {affected}.");
    }
}
