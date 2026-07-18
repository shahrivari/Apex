using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class SubsidiaryAccountWriteRepository(IGeneralConnectionFactory factory)
    : ISubsidiaryAccountWriteRepository
{
    private const string Columns = """
        id Id,
        general_account_id GeneralAccountId,
        code Code,
        name Name,
        nature Nature,
        detail_account_type DetailAccountType,
        status Status,
        created_at CreatedAt,
        updated_at UpdatedAt,
        archived_at ArchivedAt
        """;

    public async Task<SubsidiaryAccount?> GetForUpdateAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<SubsidiaryAccountRow>(new CommandDefinition(
            $"SELECT {Columns} FROM subsidiary_account WITH (UPDLOCK, ROWLOCK) WHERE id = @Id",
            new { Id = id }, factory.Transaction, cancellationToken: ct));
        return row is null
            ? null
            : SubsidiaryAccount.Rehydrate(row.Id, row.GeneralAccountId, row.Code, row.Name,
                row.Nature.ToAccountNature(), row.DetailAccountType.ToDetailAccountType(), row.Status.ToAccountStatus(),
                row.CreatedAt, row.UpdatedAt, row.ArchivedAt);
    }

    public async Task<bool> CodeExistsAsync(
        long parentId, string code, long? excludingId = null, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM subsidiary_account WITH (UPDLOCK, HOLDLOCK)
            WHERE general_account_id = @ParentId AND code = @Code AND (@ExcludingId IS NULL OR id <> @ExcludingId)
            """, new { ParentId = parentId, Code = code, ExcludingId = excludingId },
            factory.Transaction, cancellationToken: ct));
        return count > 0;
    }

    public async Task InsertAsync(SubsidiaryAccount value, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO subsidiary_account(id, general_account_id, code, name, nature, detail_account_type, status, created_at)
            VALUES(@Id, @GeneralAccountId, @Code, @Name, @Nature, @DetailType, @Status, @CreatedAt)
            """,
            new
            {
                value.Id, value.GeneralAccountId, value.Code, value.Name,
                Nature = value.Nature.ToDatabaseValue(), DetailType = value.DetailAccountType.ToDatabaseValue(),
                Status = value.Status.ToDatabaseValue(), value.CreatedAt
            }, factory.Transaction, cancellationToken: ct));
        EnsureOneRow(affected);
    }

    public async Task UpdateAsync(SubsidiaryAccount value, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE subsidiary_account SET name = @Name, status = @Status, updated_at = @UpdatedAt, archived_at = @ArchivedAt WHERE id = @Id",
            new { value.Id, value.Name, Status = value.Status.ToDatabaseValue(), value.UpdatedAt, value.ArchivedAt },
            factory.Transaction, cancellationToken: ct));
        EnsureOneRow(affected);
    }

    private static void EnsureOneRow(int affected)
    {
        if (affected != 1)
            throw new InvalidOperationException($"Expected one subsidiary account row to be affected, but affected {affected}.");
    }
}
