using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class AccountClassWriteRepository(IGeneralConnectionFactory factory) : IAccountClassWriteRepository
{
    private const string Columns = """
        id Id,
        code Code,
        name Name,
        status Status,
        created_at CreatedAt,
        updated_at UpdatedAt,
        archived_at ArchivedAt
        """;

    public async Task<AccountClass?> GetForUpdateAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<AccountClassRow>(new CommandDefinition(
            $"SELECT {Columns} FROM account_class WITH (UPDLOCK, ROWLOCK) WHERE id = @Id",
            new { Id = id }, factory.Transaction, cancellationToken: ct));
        return row is null
            ? null
            : AccountClass.Rehydrate(row.Id, row.Code, row.Name, row.Status.ToAccountStatus(),
                row.CreatedAt, row.UpdatedAt, row.ArchivedAt);
    }

    public async Task<bool> CodeExistsAsync(string code, long? excludingId = null, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM account_class WITH (UPDLOCK, HOLDLOCK)
            WHERE code = @Code AND (@ExcludingId IS NULL OR id <> @ExcludingId)
            """, new { Code = code, ExcludingId = excludingId }, factory.Transaction, cancellationToken: ct));
        return count > 0;
    }

    public async Task<bool> HasActiveChildrenAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM general_account WITH (UPDLOCK, HOLDLOCK)
            WHERE account_class_id = @Id AND status = 'ACTIVE'
            """, new { Id = id }, factory.Transaction, cancellationToken: ct));
        return count > 0;
    }

    public async Task InsertAsync(AccountClass value, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO account_class(id, code, name, status, created_at) VALUES(@Id, @Code, @Name, @Status, @CreatedAt)",
            new { value.Id, value.Code, value.Name, Status = value.Status.ToDatabaseValue(), value.CreatedAt },
            factory.Transaction, cancellationToken: ct));
        EnsureOneRow(affected);
    }

    public async Task UpdateAsync(AccountClass value, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE account_class SET name = @Name, status = @Status, updated_at = @UpdatedAt, archived_at = @ArchivedAt WHERE id = @Id",
            new { value.Id, value.Name, Status = value.Status.ToDatabaseValue(), value.UpdatedAt, value.ArchivedAt },
            factory.Transaction, cancellationToken: ct));
        EnsureOneRow(affected);
    }

    private static void EnsureOneRow(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException($"Expected one account class row to be affected, but affected {affected}.");
    }
}
