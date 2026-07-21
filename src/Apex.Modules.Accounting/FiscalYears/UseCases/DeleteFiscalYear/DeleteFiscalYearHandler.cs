using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.DeleteFiscalYear;

public sealed class DeleteFiscalYearHandler(
    IFiscalYearWriteRepository writeRepository, IFiscalYearDirectoryRepository directoryRepository,
    IShardConnectionFactory shardConnectionFactory,
    IShardKeyFactory<long> shardKeyFactory, FiscalYearDirectorySynchronizer directorySynchronizer)
{
    public async Task HandleAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(id), beginTransaction: true, cancellationToken);
        var fiscalYear = await writeRepository.GetByIdForUpdateAsync(shard, id, cancellationToken)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
        fiscalYear.EnsureCanDelete();
        if (await directoryRepository.WouldHaveGapWithoutAsync(
                fiscalYear.AccountingBookId, fiscalYear.Id, cancellationToken))
            throw new ConflictException("Deleting the fiscal year would create a date gap.",
                FiscalYearErrors.DatesHaveGap);
        await writeRepository.DeleteAsync(shard, id, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        await directorySynchronizer.DeleteBestEffortAsync(id, cancellationToken);
    }
}
