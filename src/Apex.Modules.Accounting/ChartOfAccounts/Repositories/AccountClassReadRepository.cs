using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class AccountClassReadRepository(IGeneralConnectionFactory factory) : IAccountClassReadRepository
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

    public async Task<AccountClassRow?> GetAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AccountClassRow>(new CommandDefinition(
            $"SELECT {Columns} FROM account_class WHERE id = @Id",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AccountClassRow>> ListAsync(bool includeArchived, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<AccountClassRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM account_class
            WHERE @IncludeArchived = 1 OR status = 'ACTIVE'
            ORDER BY code, id
            """, new { IncludeArchived = includeArchived }, cancellationToken: ct));
        return rows.AsList();
    }
}
