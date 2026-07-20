using Apex.Application.Abstractions.Data;
using Apex.Modules.Accounting.FiscalYears.Domain;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public interface IFiscalYearWriteRepository
{
    Task InsertAsync(IShardConnection connection, FiscalYear fiscalYear,
        CancellationToken cancellationToken = default);
    Task<FiscalYear?> GetByIdForUpdateAsync(IShardConnection connection, long id,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(IShardConnection connection, FiscalYear fiscalYear,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(IShardConnection connection, long id,
        CancellationToken cancellationToken = default);
}
