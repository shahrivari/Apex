using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public sealed class FinalizeFiscalYearHandler(
    IGeneralTransactionRunner transactionRunner, IFiscalYearWriteRepository writeRepository,
    IClock clock, IValidator<FinalizeFiscalYearRequest> validator)
{
    public async Task<FinalizeFiscalYearResponse> HandleAsync(long id, FinalizeFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        FinalizeFiscalYearResponse? response = null;
        await transactionRunner.ExecuteAsync(async ct =>
        {
            var fiscalYear = await writeRepository.GetByIdForUpdateAsync(id, ct)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
            fiscalYear.FinalizeThrough(request.FinalizedThroughDate, clock.UtcNow);
            await writeRepository.UpdateAsync(fiscalYear, ct);
            response = new FinalizeFiscalYearResponse(fiscalYear.Id, fiscalYear.FinalizedThroughDate, fiscalYear.UpdatedAt);
        }, cancellationToken);
        return response!;
    }
}
