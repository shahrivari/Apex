using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

public sealed class CreateAccountingBookHandler(
    IWriteTransactionRunner transactionRunner,
    AccountingBookWriteRepository writeRepository,
    IIdGenerator idGenerator,
    IClock clock,
    IValidator<CreateAccountingBookRequest> validator)
{
    public async Task<CreateAccountingBookResponse> HandleAsync(
        CreateAccountingBookRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var normalizedOwnerType = request.OwnerType.Trim().ToUpperInvariant();
        var normalizedOwnerId = request.OwnerId.Trim();

        CreateAccountingBookResponse? response = null;

        await transactionRunner.ExecuteAsync(AccountingModule.Name, async ct =>
        {
            var codeExists = await writeRepository.ExistsByCodeForUpdateAsync(normalizedCode, ct);
            if (codeExists)
            {
                throw new ConflictException(
                    $"Accounting book with code '{normalizedCode}' already exists.",
                    AccountingBookErrors.AccountingBookCodeAlreadyExists);
            }

            var ownerExists = await writeRepository.ExistsByOwnerForUpdateAsync(normalizedOwnerType, normalizedOwnerId, ct);
            if (ownerExists)
            {
                throw new ConflictException(
                    $"Accounting book for owner '{normalizedOwnerType}:{normalizedOwnerId}' already exists.",
                    AccountingBookErrors.AccountingBookOwnerAlreadyExists);
            }

            var book = AccountingBook.Create(
                idGenerator.NewId(),
                normalizedCode,
                request.Title,
                normalizedOwnerType,
                normalizedOwnerId,
                clock.UtcNow);

            await writeRepository.InsertAsync(book, ct);

            response = new CreateAccountingBookResponse(
                book.Id,
                book.Code,
                book.Status.ToDatabaseValue());
        }, cancellationToken);

        return response!;
    }
}
