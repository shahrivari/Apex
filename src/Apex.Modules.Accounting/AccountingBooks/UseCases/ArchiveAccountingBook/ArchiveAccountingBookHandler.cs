using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;

public sealed class ArchiveAccountingBookHandler
{
    private readonly IWriteTransactionRunner _transactionRunner;
    private readonly AccountingBookWriteRepository _writeRepository;
    private readonly IClock _clock;

    public ArchiveAccountingBookHandler(
        IWriteTransactionRunner transactionRunner,
        AccountingBookWriteRepository writeRepository,
        IClock clock)
    {
        _transactionRunner = transactionRunner;
        _writeRepository = writeRepository;
        _clock = clock;
    }

    public async Task<ArchiveAccountingBookResponse> HandleAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        ArchiveAccountingBookResponse? response = null;

        await _transactionRunner.ExecuteAsync(AccountingModule.Name, async ct =>
        {
            var book = await _writeRepository.GetByIdForUpdateAsync(id, ct);

            if (book == null)
            {
                throw new NotFoundException(
                    $"Accounting book '{id}' was not found.",
                    AccountingBookErrors.AccountingBookNotFound);
            }

            book.Archive(_clock.UtcNow);

            await _writeRepository.UpdateStatusAsync(book, ct);

            response = new ArchiveAccountingBookResponse(
                book.Id,
                book.Code,
                book.Status.ToDatabaseValue(),
                book.ArchivedAt);
        }, cancellationToken);

        return response!;
    }
}
