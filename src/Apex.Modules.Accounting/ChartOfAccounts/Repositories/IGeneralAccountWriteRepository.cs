using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface IGeneralAccountWriteRepository
{
    Task<GeneralAccount?> GetForUpdateAsync(long id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(
        long parentId, string code, long? excludingId = null, CancellationToken ct = default);
    Task<bool> HasActiveChildrenAsync(long id, CancellationToken ct = default);
    Task InsertAsync(GeneralAccount value, CancellationToken ct = default);
    Task UpdateAsync(GeneralAccount value, CancellationToken ct = default);
}
