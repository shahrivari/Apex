using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;

public sealed class CancelFiscalYearHandler(
    IGeneralTransactionRunner transactionRunner, IFiscalYearWriteRepository writeRepository,
    IClock clock, IValidator<CancelFiscalYearRequest> validator)
{
    public async Task<CancelFiscalYearResponse> HandleAsync(long id, CancelFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        CancelFiscalYearResponse? response = null;
        await transactionRunner.ExecuteAsync(async ct =>
        {
            var fiscalYear = await writeRepository.GetByIdForUpdateAsync(id, ct)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
            fiscalYear.Cancel(request.CancellationDate, clock.UtcNow);
            await writeRepository.UpdateAsync(fiscalYear, ct);
            response = new CancelFiscalYearResponse(fiscalYear.Id, fiscalYear.Status.ToDatabaseValue(),
                fiscalYear.CancellationDate, fiscalYear.CancelledAt);
        }, cancellationToken);
        return response!;
    }
}
