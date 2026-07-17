using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;

public sealed class CreateFiscalYearHandler(
    IGeneralTransactionRunner transactionRunner, IFiscalYearWriteRepository writeRepository,
    IAccountingBookWriteRepository accountingBookRepository,
    IIdGenerator idGenerator, IClock clock, IValidator<CreateFiscalYearRequest> validator)
{
    public async Task<CreateFiscalYearResponse> HandleAsync(CreateFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        CreateFiscalYearResponse? response = null;
        await transactionRunner.ExecuteAsync(async ct =>
        {
            var accountingBook = await accountingBookRepository.GetByIdForUpdateAsync(request.AccountingBookId, ct);
            if (accountingBook is null)
                throw new NotFoundException("Accounting book was not found.", AccountingBookErrors.AccountingBookNotFound);
            if (accountingBook.Status == AccountingBookStatus.Archived)
                throw new BusinessRuleException("A fiscal year cannot be created for an archived accounting book.",
                    FiscalYearErrors.AccountingBookArchived);
            if (await writeRepository.HasOverlapForUpdateAsync(
                    request.AccountingBookId, request.StartDate, request.EndDate, cancellationToken: ct))
                throw new ConflictException("The fiscal year dates overlap another fiscal year.", FiscalYearErrors.DatesOverlap);

            var fiscalYear = FiscalYear.Create(idGenerator.NewId(), request.AccountingBookId, request.Title,
                request.StartDate, request.EndDate, clock.UtcNow);
            await writeRepository.InsertAsync(fiscalYear, ct);
            response = new CreateFiscalYearResponse(
                fiscalYear.Id, fiscalYear.AccountingBookId, fiscalYear.Title, fiscalYear.StartDate,
                fiscalYear.EndDate, fiscalYear.Status.ToDatabaseValue(), fiscalYear.FinalizedThroughDate,
                fiscalYear.NextDocumentNumber);
        }, cancellationToken);
        return response!;
    }
}
