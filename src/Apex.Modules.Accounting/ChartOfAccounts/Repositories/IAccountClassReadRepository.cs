using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface IAccountClassReadRepository
{
    Task<AccountClassRow?> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<AccountClassRow>> ListAsync(bool includeArchived, CancellationToken ct = default);
}
