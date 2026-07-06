using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Exceptions;
using Apex.Application.Abstractions.Ids;
using Apex.Application.Abstractions.Time;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

public sealed class CreateAccountingBookHandler
{
    private readonly IWriteTransactionRunner _transactionRunner;
    private readonly AccountingBookWriteRepository _writeRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly IValidator<CreateAccountingBookRequest> _validator;

    public CreateAccountingBookHandler(
        IWriteTransactionRunner transactionRunner,
        AccountingBookWriteRepository writeRepository,
        IIdGenerator idGenerator,
        IClock clock,
        IValidator<CreateAccountingBookRequest> validator)
    {
        _transactionRunner = transactionRunner;
        _writeRepository = writeRepository;
        _idGenerator = idGenerator;
        _clock = clock;
        _validator = validator;
    }

    public async Task<CreateAccountingBookResponse> HandleAsync(
        CreateAccountingBookRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var normalizedOwnerType = request.OwnerType.Trim().ToUpperInvariant();
        var normalizedOwnerId = request.OwnerId.Trim();

        CreateAccountingBookResponse? response = null;

        await _transactionRunner.ExecuteAsync(AccountingModule.Name, async ct =>
        {
            var codeExists = await _writeRepository.ExistsByCodeForUpdateAsync(normalizedCode, ct);
            if (codeExists)
            {
                throw new ConflictException(
                    $"Accounting book with code '{normalizedCode}' already exists.",
                    AccountingBookErrors.AccountingBookCodeAlreadyExists);
            }

            var ownerExists = await _writeRepository.ExistsByOwnerForUpdateAsync(normalizedOwnerType, normalizedOwnerId, ct);
            if (ownerExists)
            {
                throw new ConflictException(
                    $"Accounting book for owner '{normalizedOwnerType}:{normalizedOwnerId}' already exists.",
                    AccountingBookErrors.AccountingBookOwnerAlreadyExists);
            }

            var book = AccountingBook.Create(
                _idGenerator.NewId(),
                normalizedCode,
                request.Title,
                normalizedOwnerType,
                normalizedOwnerId,
                _clock.UtcNow);

            await _writeRepository.InsertAsync(book, ct);

            response = new CreateAccountingBookResponse(
                book.Id,
                book.Code,
                book.Status.ToDatabaseValue());
        }, cancellationToken);

        return response!;
    }
}
