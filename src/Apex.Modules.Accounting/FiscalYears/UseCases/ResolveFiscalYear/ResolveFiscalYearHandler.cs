using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;

public sealed class ResolveFiscalYearHandler(
    IFiscalYearReadRepository readRepository, IValidator<ResolveFiscalYearRequest> validator)
{
    public async Task<ResolveFiscalYearResponse> HandleAsync(ResolveFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var row = await readRepository.ResolveForDateAsync(request.AccountingBookId, request.AccountingDate,
            request.RequiredStatus?.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new NotFoundException("No eligible fiscal year contains the accounting date.",
                FiscalYearErrors.NotFoundForDate);
        return new ResolveFiscalYearResponse(row.Id, row.AccountingBookId, row.Title, row.StartDate,
            row.CancellationDate ?? row.EndDate, row.Status, row.FinalizedThroughDate);
    }
}
