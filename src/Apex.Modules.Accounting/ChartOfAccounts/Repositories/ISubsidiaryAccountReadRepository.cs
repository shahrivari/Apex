using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface ISubsidiaryAccountReadRepository
{
    Task<SubsidiaryAccountRow?> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<SubsidiaryAccountRow>> ListAsync(bool includeArchived, CancellationToken ct = default);
}
