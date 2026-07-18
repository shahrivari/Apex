using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.DetailAccounts.Repositories;

public sealed class DetailAccountWriteRepository(IGeneralConnectionFactory factory)
    : IDetailAccountWriteRepository
{
    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        (
            await (await factory.OpenAsync(ct)).ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT (SELECT COUNT(1) FROM detail_account WITH (UPDLOCK,HOLDLOCK) WHERE code=@Code) + (SELECT COUNT(1) FROM detail_account_retired_code WITH (UPDLOCK,HOLDLOCK) WHERE code=@Code)",
                    new { Code = code },
                    factory.Transaction,
                    cancellationToken: ct
                )
            )
        ) > 0;

    public async Task<DetailAccount?> GetForUpdateAsync(long id, CancellationToken ct = default)
    {
        var row = await (await factory.OpenAsync(ct)).QuerySingleOrDefaultAsync<DetailAccountRow>(
            new CommandDefinition(
                "SELECT id AS Id,code AS Code,name AS Name,type AS Type,status AS Status,created_at AS CreatedAt,updated_at AS UpdatedAt,archived_at AS ArchivedAt FROM detail_account WITH (UPDLOCK,ROWLOCK) WHERE id=@Id",
                new { Id = id },
                factory.Transaction,
                cancellationToken: ct
            )
        );
        return row is null
            ? null
            : DetailAccount.Rehydrate(
                row.Id,
                row.Code,
                row.Name,
                DetailAccountValues.ParseType(row.Type),
                DetailAccountValues.ParseStatus(row.Status),
                row.CreatedAt,
                row.UpdatedAt,
                row.ArchivedAt
            );
    }

    public async Task InsertAsync(DetailAccount a, CancellationToken ct = default) =>
        EnsureOne(
            await (await factory.OpenAsync(ct)).ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO detail_account(id,code,name,type,status,created_at) VALUES(@Id,@Code,@Name,@Type,@Status,@CreatedAt)",
                    new
                    {
                        a.Id,
                        a.Code,
                        a.Name,
                        Type = a.Type.ToDatabaseValue(),
                        Status = a.Status.ToDatabaseValue(),
                        a.CreatedAt,
                    },
                    factory.Transaction,
                    cancellationToken: ct
                )
            )
        );

    public async Task UpdateAsync(DetailAccount a, CancellationToken ct = default) =>
        EnsureOne(
            await (await factory.OpenAsync(ct)).ExecuteAsync(
                new CommandDefinition(
                    "UPDATE detail_account SET name=@Name,type=@Type,status=@Status,updated_at=@UpdatedAt,archived_at=@ArchivedAt WHERE id=@Id",
                    new
                    {
                        a.Id,
                        a.Name,
                        Type = a.Type.ToDatabaseValue(),
                        Status = a.Status.ToDatabaseValue(),
                        a.UpdatedAt,
                        a.ArchivedAt,
                    },
                    factory.Transaction,
                    cancellationToken: ct
                )
            )
        );

    public async Task DeleteAsync(DetailAccount a, CancellationToken ct = default)
    {
        var c = await factory.OpenAsync(ct);
        await c.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO detail_account_retired_code(code,retired_at) VALUES(@Code,@RetiredAt)",
                new { a.Code, RetiredAt = a.UpdatedAt ?? a.CreatedAt },
                factory.Transaction,
                cancellationToken: ct
            )
        );
        EnsureOne(
            await c.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM detail_account WHERE id=@Id",
                    new { a.Id },
                    factory.Transaction,
                    cancellationToken: ct
                )
            )
        );
    }

    private static void EnsureOne(int count)
    {
        if (count != 1)
            throw new InvalidOperationException(
                "Detail account persistence affected an unexpected number of rows."
            );
    }
}
