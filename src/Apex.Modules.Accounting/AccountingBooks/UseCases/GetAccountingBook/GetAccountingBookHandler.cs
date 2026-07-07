using Apex.Application.Abstractions.Exceptions;
using Apex.Modules.Accounting.AccountingBooks.Domain;
using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.AccountingBooks.SqlModels;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;

public sealed class GetAccountingBookHandler(AccountingBookReadRepository readRepository)
{
    public async Task<GetAccountingBookResponse> HandleAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var model = await readRepository.GetByIdAsync(id, cancellationToken);

        if (model == null)
        {
            throw new NotFoundException(
                $"Accounting book '{id}' was not found.",
                AccountingBookErrors.AccountingBookNotFound);
        }

        return MapResponse(model);
    }

    static GetAccountingBookResponse MapResponse(AccountingBookSqlModel model)
    {
        return new GetAccountingBookResponse(
            model.Id,
            model.Code,
            model.Title,
            model.OwnerType,
            model.OwnerId,
            model.Status,
            model.CreatedAt,
            model.UpdatedAt,
            model.ActivatedAt,
            model.SuspendedAt,
            model.ArchivedAt);
    }
}
