using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class SubsidiaryAccountReadRepository(IGeneralConnectionFactory factory)
    : ISubsidiaryAccountReadRepository
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

    public async Task<SubsidiaryAccountRow?> GetAsync(long id, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SubsidiaryAccountRow>(new CommandDefinition(
            $"SELECT {Columns} FROM subsidiary_account WHERE id = @Id",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SubsidiaryAccountRow>> ListAsync(
        bool includeArchived, CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<SubsidiaryAccountRow>(new CommandDefinition(
            $"""
            SELECT {Columns}
            FROM subsidiary_account
            WHERE @IncludeArchived = 1 OR status = 'ACTIVE'
            ORDER BY code, id
            """, new { IncludeArchived = includeArchived }, cancellationToken: ct));
        return rows.AsList();
    }
}
