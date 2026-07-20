using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;
using Dapper;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal sealed class AccountPathReadRepository(IGeneralConnectionFactory factory)
    : IAccountPathReadRepository
{
    public async Task<AccountPathRow?> ResolveAsync(
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        CancellationToken ct = default)
    {
        var connection = await factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AccountPathRow>(new CommandDefinition(
            """
            SELECT
                c.status AS ClassStatus,
                g.status AS GeneralStatus,
                s.status AS SubsidiaryStatus,
                s.detail_account_type AS DetailAccountType
            FROM subsidiary_account s
            INNER JOIN general_account g ON g.id = s.general_account_id
            INNER JOIN account_class c ON c.id = g.account_class_id
            WHERE c.code = @ClassCode AND g.code = @GeneralCode AND s.code = @SubsidiaryCode
            """,
            new
            {
                ClassCode = accountClassCode,
                GeneralCode = generalAccountCode,
                SubsidiaryCode = subsidiaryAccountCode
            },
            cancellationToken: ct));
    }
}
