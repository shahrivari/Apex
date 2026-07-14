using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;

public sealed class ArchiveAccountingBookHandler(
    IGeneralTransactionRunner transactionRunner,
    IAccountingBookWriteRepository writeRepository,
    IClock clock)
{
    public async Task<ArchiveAccountingBookResponse> HandleAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        ArchiveAccountingBookResponse? response = null;

        await transactionRunner.ExecuteAsync(async ct =>
        {
            var book = await writeRepository.GetByIdForUpdateAsync(id, ct);

            if (book == null)
            {
                throw new NotFoundException(
                    $"Accounting book '{id}' was not found.",
                    AccountingBookErrors.AccountingBookNotFound);
            }

            book.Archive(clock.UtcNow);

            await writeRepository.UpdateStatusAsync(book, ct);

            response = new ArchiveAccountingBookResponse(
                book.Id,
                book.Code,
                book.Status.ToDatabaseValue(),
                book.ArchivedAt);
        }, cancellationToken);

        return response!;
    }
}
