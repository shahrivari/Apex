using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface ISubsidiaryAccountWriteRepository
{
    Task<SubsidiaryAccount?> GetForUpdateAsync(long id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(
        long parentId, string code, long? excludingId = null, CancellationToken ct = default);
    Task InsertAsync(SubsidiaryAccount value, CancellationToken ct = default);
    Task UpdateAsync(SubsidiaryAccount value, CancellationToken ct = default);
}
