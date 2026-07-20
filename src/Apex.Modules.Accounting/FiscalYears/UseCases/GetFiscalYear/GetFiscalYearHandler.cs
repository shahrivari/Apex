using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;

public sealed class GetFiscalYearHandler(IFiscalYearReadRepository readRepository)
{
    public async Task<GetFiscalYearResponse> HandleAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await readRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
        return new GetFiscalYearResponse(row.Id, row.AccountingBookId, row.Title, row.StartDate, row.EndDate,
            row.Status, row.FinalizedThroughDate, row.NextReferenceNumber, row.NextJournalEntryNumber,
            row.CreatedAt, row.UpdatedAt,
            row.OpenedAt, row.ClosedAt, row.CancelledAt, row.CancellationDate);
    }
}
