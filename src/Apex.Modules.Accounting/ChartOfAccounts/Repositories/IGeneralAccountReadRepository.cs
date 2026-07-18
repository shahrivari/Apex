using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface IGeneralAccountReadRepository
{
    Task<GeneralAccountRow?> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<GeneralAccountRow>> ListAsync(bool includeArchived, CancellationToken ct = default);
}
