using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public sealed class FinalizeFiscalYearHandler(
    IFiscalYearWriteRepository writeRepository, IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory, FiscalYearDirectorySynchronizer directorySynchronizer,
    IClock clock, IValidator<FinalizeFiscalYearRequest> validator)
{
    public async Task<FinalizeFiscalYearResponse> HandleAsync(long id, FinalizeFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(id), beginTransaction: true, cancellationToken);
        var fiscalYear = await writeRepository.GetByIdForUpdateAsync(shard, id, cancellationToken)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
        fiscalYear.FinalizeThrough(request.FinalizedThroughDate, clock.UtcNow);
        await writeRepository.UpdateAsync(shard, fiscalYear, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        await directorySynchronizer.UpsertBestEffortAsync(fiscalYear, cancellationToken);
        return new FinalizeFiscalYearResponse(
            fiscalYear.Id, fiscalYear.FinalizedThroughDate, fiscalYear.UpdatedAt);
    }
}
