using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

public sealed class UpdateFiscalYearHandler(
    IFiscalYearWriteRepository writeRepository, IFiscalYearDirectoryRepository directoryRepository,
    IShardConnectionFactory shardConnectionFactory, IShardKeyFactory<long> shardKeyFactory,
    FiscalYearDirectorySynchronizer directorySynchronizer,
    IClock clock, IValidator<UpdateFiscalYearRequest> validator)
{
    public async Task<UpdateFiscalYearResponse> HandleAsync(long id, UpdateFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(id), beginTransaction: true, cancellationToken);
        var fiscalYear = await writeRepository.GetByIdForUpdateAsync(shard, id, cancellationToken)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
        fiscalYear.EnsureCanUpdate();
        if (await directoryRepository.HasOverlapAsync(fiscalYear.AccountingBookId,
                request.StartDate, request.EndDate, fiscalYear.Id, cancellationToken))
            throw new ConflictException("The fiscal year dates overlap another fiscal year.", FiscalYearErrors.DatesOverlap);
        if (await directoryRepository.WouldHaveGapWithRangeAsync(fiscalYear.AccountingBookId,
                request.StartDate, request.EndDate, fiscalYear.Id, cancellationToken))
            throw new ConflictException("Fiscal years must form a contiguous date range.",
                FiscalYearErrors.DatesHaveGap);
        fiscalYear.UpdateDraft(request.Title, request.StartDate, request.EndDate, clock.UtcNow);
        await writeRepository.UpdateAsync(shard, fiscalYear, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        await directorySynchronizer.UpsertBestEffortAsync(fiscalYear, cancellationToken);
        return new UpdateFiscalYearResponse(fiscalYear.Id, fiscalYear.Title, fiscalYear.StartDate,
                fiscalYear.EndDate, fiscalYear.FinalizedThroughDate, fiscalYear.UpdatedAt);
    }
}
