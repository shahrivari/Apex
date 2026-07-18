using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class GeneralAccountReadRepository(IGeneralConnectionFactory factory) : IGeneralAccountReadRepository
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

    public async Task<GeneralAccountRow?> GetAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<GeneralAccountRow>(new CommandDefinition(
            $"SELECT {Columns} FROM general_account WHERE id = @Id",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<GeneralAccountRow>> ListAsync(
        bool includeArchived, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<GeneralAccountRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM general_account
            WHERE @IncludeArchived = 1 OR status = 'ACTIVE'
            ORDER BY code, id
            """, new { IncludeArchived = includeArchived }, cancellationToken: ct));
        return rows.AsList();
    }
}
