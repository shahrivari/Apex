using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.FiscalYears.Repositories;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;

public sealed class OpenFiscalYearHandler(
    IFiscalYearWriteRepository writeRepository, IFiscalYearDirectoryRepository directoryRepository,
    IAccountingBookReadRepository accountingBookRepository,
    IShardConnectionFactory shardConnectionFactory, IShardKeyFactory<long> shardKeyFactory,
    FiscalYearDirectorySynchronizer directorySynchronizer, IClock clock)
{
    public async Task<OpenFiscalYearResponse> HandleAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var shard = await shardConnectionFactory.OpenAsync(
            shardKeyFactory.Create(id), beginTransaction: true, cancellationToken);
        var fiscalYear = await writeRepository.GetByIdForUpdateAsync(shard, id, cancellationToken)
                ?? throw new NotFoundException("Fiscal year was not found.", FiscalYearErrors.NotFound);
        var accountingBook = await accountingBookRepository.GetByIdAsync(fiscalYear.AccountingBookId, cancellationToken)
            ?? throw new NotFoundException("Accounting book was not found.", AccountingBookErrors.AccountingBookNotFound);
        if (accountingBook.Status != AccountingBookStatus.Active.ToDatabaseValue())
            throw new BusinessRuleException("A fiscal year can be opened only for an active accounting book.",
                FiscalYearErrors.AccountingBookNotActive);
        fiscalYear.Open(clock.UtcNow);
        if (await directoryRepository.HasOtherOpenAsync(fiscalYear.AccountingBookId, fiscalYear.Id, cancellationToken))
            throw new ConflictException("Another fiscal year is already open for the accounting book.",
                FiscalYearErrors.OpenAlreadyExists);
        await writeRepository.UpdateAsync(shard, fiscalYear, cancellationToken);
        await shard.Transaction!.CommitAsync(cancellationToken);
        await directorySynchronizer.UpsertBestEffortAsync(fiscalYear, cancellationToken);
        return new OpenFiscalYearResponse(fiscalYear.Id, fiscalYear.Status.ToDatabaseValue(), fiscalYear.OpenedAt);
    }
}
