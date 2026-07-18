using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface IAccountClassWriteRepository
{
    Task<AccountClass?> GetForUpdateAsync(long id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, long? excludingId = null, CancellationToken ct = default);
    Task<bool> HasActiveChildrenAsync(long id, CancellationToken ct = default);
    Task InsertAsync(AccountClass value, CancellationToken ct = default);
    Task UpdateAsync(AccountClass value, CancellationToken ct = default);
}
