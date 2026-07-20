using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.RepairFiscalYearDirectory;

public sealed class RepairFiscalYearDirectoryHandler(
    IFiscalYearReadRepository readRepository,
    IFiscalYearDirectoryRepository directoryRepository,
    IClock clock)
{
    public async Task HandleAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await readRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);

        await directoryRepository.UpsertAsync(FiscalYearWriteRepository.Map(row), clock.UtcNow, cancellationToken);
    }
}
