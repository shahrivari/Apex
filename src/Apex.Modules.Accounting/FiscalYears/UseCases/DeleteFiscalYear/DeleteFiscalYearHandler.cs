using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.DeleteFiscalYear;

public sealed class DeleteFiscalYearHandler(
    IGeneralTransactionRunner transactionRunner, IFiscalYearWriteRepository writeRepository)
{
    public async Task HandleAsync(long id, CancellationToken cancellationToken = default)
    {
        await transactionRunner.ExecuteAsync(async ct =>
        {
            var fiscalYear = await writeRepository.GetByIdForUpdateAsync(id, ct)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
            fiscalYear.EnsureCanDelete();
            await writeRepository.DeleteAsync(id, ct);
        }, cancellationToken);
    }
}
