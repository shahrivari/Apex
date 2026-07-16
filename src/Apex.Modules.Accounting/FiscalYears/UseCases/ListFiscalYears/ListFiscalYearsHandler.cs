using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;

public sealed class ListFiscalYearsHandler(
    IFiscalYearReadRepository readRepository, IValidator<ListFiscalYearsRequest> validator)
{
    public async Task<ListFiscalYearsResponse> HandleAsync(ListFiscalYearsRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var status = request.Status?.Trim().ToUpperInvariant();
        var (rows, totalCount) = await readRepository.ListAsync(request.AccountingBookId, status,
            request.FromDate, request.ToDate, request.Page, request.PageSize, cancellationToken);
        var items = rows.Select(row => new FiscalYearItem(row.Id, row.AccountingBookId, row.Title,
            row.StartDate, row.EndDate, row.Status, row.FinalizedThroughDate,
            row.NextDocumentNumber, row.CancellationDate)).ToList();
        return new ListFiscalYearsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
