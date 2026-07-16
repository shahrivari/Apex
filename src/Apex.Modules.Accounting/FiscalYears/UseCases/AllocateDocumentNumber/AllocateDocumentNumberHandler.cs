using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.AllocateDocumentNumber;

public sealed class AllocateDocumentNumberHandler(
    IGeneralTransactionRunner transactionRunner,
    IFiscalYearWriteRepository writeRepository)
{
    public async Task<long> HandleAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        return await transactionRunner.ExecuteAsync(async ct =>
            await writeRepository.AllocateDocumentNumberAsync(fiscalYearId, ct)
            ?? throw new NotFoundException(
                "An eligible fiscal year was not found for document-number allocation.",
                FiscalYearErrors.NotFound), cancellationToken);
    }
}
