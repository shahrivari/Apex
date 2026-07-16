using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;

public sealed class OpenFiscalYearHandler(
    IGeneralTransactionRunner transactionRunner, IFiscalYearWriteRepository writeRepository, IClock clock)
{
    public async Task<OpenFiscalYearResponse> HandleAsync(long id, CancellationToken cancellationToken = default)
    {
        OpenFiscalYearResponse? response = null;
        await transactionRunner.ExecuteAsync(async ct =>
        {
            var fiscalYear = await writeRepository.GetByIdForUpdateAsync(id, ct)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
            fiscalYear.Open(clock.UtcNow);
            if (await writeRepository.HasOtherOpenForUpdateAsync(fiscalYear.AccountingBookId, fiscalYear.Id, ct))
                throw new ConflictException("Another fiscal year is already open for the accounting book.",
                    FiscalYearErrors.OpenAlreadyExists);
            await writeRepository.UpdateAsync(fiscalYear, ct);
            response = new OpenFiscalYearResponse(fiscalYear.Id, fiscalYear.Status.ToDatabaseValue(), fiscalYear.OpenedAt);
        }, cancellationToken);
        return response!;
    }
}
