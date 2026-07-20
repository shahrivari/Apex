using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public interface IFiscalYearReadRepository
{
    Task<FiscalYearRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
