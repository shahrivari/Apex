using Apex.Modules.Accounting.DetailAccounts.Domain;

namespace Apex.Modules.Accounting.DetailAccounts.Repositories;

public interface IDetailAccountWriteRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task<DetailAccount?> GetForUpdateAsync(long id, CancellationToken ct = default);
    Task InsertAsync(DetailAccount account, CancellationToken ct = default);
    Task UpdateAsync(DetailAccount account, CancellationToken ct = default);
    Task DeleteAsync(DetailAccount account, CancellationToken ct = default);
}
