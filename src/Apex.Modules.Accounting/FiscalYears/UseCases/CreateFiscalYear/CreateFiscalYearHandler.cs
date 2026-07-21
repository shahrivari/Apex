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
    IFiscalYearWriteRepository writeRepository, IFiscalYearDirectoryRepository directoryRepository,
    IAccountingBookReadRepository accountingBookRepository, IShardAssignmentProvisioner provisioner,
    IShardConnectionFactory shardConnectionFactory, IShardKeyFactory<long> shardKeyFactory,
    FiscalYearDirectorySynchronizer directorySynchronizer,
    IIdGenerator idGenerator, IClock clock, IValidator<CreateFiscalYearRequest> validator)
{
    public async Task<CreateFiscalYearResponse> HandleAsync(CreateFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var accountingBook = await accountingBookRepository.GetByIdAsync(request.AccountingBookId, cancellationToken);
        if (accountingBook is null)
            throw new NotFoundException("Accounting book was not found.", AccountingBookErrors.AccountingBookNotFound);
        if (accountingBook.Status == AccountingBookStatus.Archived.ToDatabaseValue())
            throw new BusinessRuleException("A fiscal year cannot be created for an archived accounting book.",
                FiscalYearErrors.AccountingBookArchived);
        if (await directoryRepository.HasOverlapAsync(
                request.AccountingBookId, request.StartDate, request.EndDate, cancellationToken: cancellationToken))
            throw new ConflictException("The fiscal year dates overlap another fiscal year.", FiscalYearErrors.DatesOverlap);
        if (await directoryRepository.WouldHaveGapWithRangeAsync(
                request.AccountingBookId, request.StartDate, request.EndDate,
                cancellationToken: cancellationToken))
            throw new ConflictException("Fiscal years must form a contiguous date range.",
                FiscalYearErrors.DatesHaveGap);

        var fiscalYear = FiscalYear.Create(idGenerator.NewId(), request.AccountingBookId, request.Title,
            request.StartDate, request.EndDate, clock.UtcNow);
        var shardKey = shardKeyFactory.Create(fiscalYear.Id);
        await provisioner.EnsureAssignedAsync(shardKey, cancellationToken);
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKey, beginTransaction: true, cancellationToken);
        await writeRepository.InsertAsync(shard, fiscalYear, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        await directorySynchronizer.UpsertBestEffortAsync(fiscalYear, cancellationToken);
        return new CreateFiscalYearResponse(
            fiscalYear.Id, fiscalYear.AccountingBookId, fiscalYear.Title, fiscalYear.StartDate,
            fiscalYear.EndDate, fiscalYear.Status.ToDatabaseValue(), fiscalYear.FinalizedThroughDate,
            fiscalYear.NextReferenceNumber, fiscalYear.NextJournalEntryNumber);
    }
}
