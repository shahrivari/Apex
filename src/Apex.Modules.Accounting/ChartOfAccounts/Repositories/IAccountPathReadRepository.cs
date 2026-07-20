using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface IAccountPathReadRepository
{
    Task<AccountPathRow?> ResolveAsync(
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        CancellationToken ct = default);
}
