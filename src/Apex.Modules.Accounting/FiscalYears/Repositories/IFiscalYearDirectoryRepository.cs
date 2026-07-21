using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories.Rows;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public interface IFiscalYearDirectoryRepository
{
    Task<(IReadOnlyList<FiscalYearRow> Items, int TotalCount)> ListAsync(
        long? accountingBookId, string? status, DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FiscalYearRow?> ResolveForDateAsync(
        long accountingBookId, DateOnly accountingDate, string? requiredStatus,
        CancellationToken cancellationToken = default);
    Task<bool> HasOverlapAsync(
        long accountingBookId, DateOnly startDate, DateOnly endDate, long? excludedId = null,
        CancellationToken cancellationToken = default);
    Task<bool> WouldHaveGapWithRangeAsync(
        long accountingBookId, DateOnly startDate, DateOnly effectiveEndDate, long? excludedId = null,
        CancellationToken cancellationToken = default);
    Task<bool> WouldHaveGapWithoutAsync(
        long accountingBookId, long excludedId, CancellationToken cancellationToken = default);
    Task<bool> HasOtherOpenAsync(
        long accountingBookId, long excludedId, CancellationToken cancellationToken = default);
    Task UpsertAsync(FiscalYear fiscalYear, DateTime syncedAt, CancellationToken cancellationToken = default);
    Task DeleteAsync(long fiscalYearId, CancellationToken cancellationToken = default);
}
