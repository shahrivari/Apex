using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using Microsoft.Extensions.Logging;

namespace Apex.Modules.Accounting.FiscalYears.UseCases;

public sealed class FiscalYearDirectorySynchronizer(
    IFiscalYearDirectoryRepository directoryRepository,
    IClock clock,
    ILogger<FiscalYearDirectorySynchronizer> logger)
{
    public async Task UpsertBestEffortAsync(FiscalYear fiscalYear, CancellationToken cancellationToken)
    {
        try
        {
            await directoryRepository.UpsertAsync(fiscalYear, clock.UtcNow, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Fiscal year directory synchronization failed for {FiscalYearId}.", fiscalYear.Id);
        }
    }

    public async Task DeleteBestEffortAsync(long fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            await directoryRepository.DeleteAsync(fiscalYearId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Fiscal year directory deletion failed for {FiscalYearId}.", fiscalYearId);
        }
    }
}
