using Apex.Modules.Accounting.FiscalYears.Domain;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public interface IFiscalYearWriteRepository
{
    Task InsertAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default);
    Task<FiscalYear?> GetByIdForUpdateAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> HasOverlapForUpdateAsync(long accountingBookId, DateOnly startDate, DateOnly endDate,
        long? excludedId = null, CancellationToken cancellationToken = default);
    Task<bool> HasOtherOpenForUpdateAsync(long accountingBookId, long excludedId,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<long?> AllocateDocumentNumberAsync(long id, CancellationToken cancellationToken = default);
}
