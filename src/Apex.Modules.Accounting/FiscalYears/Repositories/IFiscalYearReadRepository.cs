using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public interface IFiscalYearReadRepository
{
    Task<FiscalYearRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FiscalYearRow> Items, int TotalCount)> ListAsync(
        long? accountingBookId, string? status, DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FiscalYearRow?> ResolveForDateAsync(
        long accountingBookId, DateOnly accountingDate, string? requiredStatus,
        CancellationToken cancellationToken = default);
}
