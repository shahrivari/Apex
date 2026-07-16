using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

public sealed class UpdateFiscalYearHandler(
    IGeneralTransactionRunner transactionRunner, IFiscalYearWriteRepository writeRepository,
    IClock clock, IValidator<UpdateFiscalYearRequest> validator)
{
    public async Task<UpdateFiscalYearResponse> HandleAsync(long id, UpdateFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        UpdateFiscalYearResponse? response = null;
        await transactionRunner.ExecuteAsync(async ct =>
        {
            var fiscalYear = await writeRepository.GetByIdForUpdateAsync(id, ct)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
            fiscalYear.EnsureCanUpdate();
            if (await writeRepository.HasOverlapForUpdateAsync(fiscalYear.AccountingBookId,
                    request.StartDate, request.EndDate, fiscalYear.Id, ct))
                throw new ConflictException("The fiscal year dates overlap another fiscal year.", FiscalYearErrors.DatesOverlap);
            fiscalYear.UpdateDraft(request.Title, request.StartDate, request.EndDate, clock.UtcNow);
            await writeRepository.UpdateAsync(fiscalYear, ct);
            response = new UpdateFiscalYearResponse(fiscalYear.Id, fiscalYear.Title, fiscalYear.StartDate,
                fiscalYear.EndDate, fiscalYear.FinalizedThroughDate, fiscalYear.UpdatedAt);
        }, cancellationToken);
        return response!;
    }
}
