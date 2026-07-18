using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.DetailAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.DetailAccounts.Repositories;

public sealed class DetailAccountReadRepository(IGeneralConnectionFactory factory)
    : IDetailAccountReadRepository
{
    private const string Columns =
        "id AS Id, code AS Code, name AS Name, type AS Type, status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt, archived_at AS ArchivedAt";

    public async Task<DetailAccountRow?> GetByIdAsync(long id, CancellationToken ct = default) =>
        await (await factory.OpenAsync(ct)).QuerySingleOrDefaultAsync<DetailAccountRow>(
            new CommandDefinition(
                $"SELECT {Columns} FROM detail_account WHERE id=@Id",
                new { Id = id },
                cancellationToken: ct
            )
        );

    public async Task<DetailAccountRow?> GetByCodeAsync(
        string code,
        CancellationToken ct = default
    ) =>
        await (await factory.OpenAsync(ct)).QuerySingleOrDefaultAsync<DetailAccountRow>(
            new CommandDefinition(
                $"SELECT {Columns} FROM detail_account WHERE code=@Code",
                new { Code = code },
                cancellationToken: ct
            )
        );

    public async Task<(IReadOnlyList<DetailAccountRow> Items, int TotalCount)> ListAsync(
        string? type,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var filters = new List<string>();
        var p = new DynamicParameters();
        if (type is not null)
        {
            filters.Add("type=@Type");
            p.Add("Type", type);
        }
        if (status is not null)
        {
            filters.Add("status=@Status");
            p.Add("Status", status);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add("(code LIKE @Search OR name LIKE @Search)");
            p.Add("Search", $"%{search.Trim()}%");
        }
        var where = filters.Count == 0 ? "" : "WHERE " + string.Join(" AND ", filters);
        p.Add("Skip", (page - 1) * pageSize);
        p.Add("Take", pageSize);
        var connection = await factory.OpenAsync(ct);
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(1) FROM detail_account {where}",
                p,
                cancellationToken: ct
            )
        );
        var rows = (
            await connection.QueryAsync<DetailAccountRow>(
                new CommandDefinition(
                    $"SELECT {Columns} FROM detail_account {where} ORDER BY code, id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                    p,
                    cancellationToken: ct
                )
            )
        ).AsList();
        return (rows, total);
    }

    public async Task<IReadOnlyList<DetailAccountRow>> SearchForPostingAsync(
        string type,
        string? search,
        int limit,
        CancellationToken ct = default
    )
    {
        var sql =
            $"SELECT TOP (@Limit) {Columns} FROM detail_account WHERE type=@Type AND status='ACTIVE' AND (@Search IS NULL OR code LIKE @Search OR name LIKE @Search) ORDER BY code, id";
        return (
            await (await factory.OpenAsync(ct)).QueryAsync<DetailAccountRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Type = type,
                        Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                        Limit = limit,
                    },
                    cancellationToken: ct
                )
            )
        ).AsList();
    }
}
