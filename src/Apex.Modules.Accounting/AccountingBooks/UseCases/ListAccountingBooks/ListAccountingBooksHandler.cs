using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.AccountingBooks.SqlModels;
using FluentValidation;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public sealed class ListAccountingBooksHandler(
    AccountingBookReadRepository readRepository,
    IValidator<ListAccountingBooksRequest> validator)
{
    public async Task<ListAccountingBooksResponse> HandleAsync(
        ListAccountingBooksRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var (items, totalCount) = await readRepository.ListAsync(
            request.Status,
            request.OwnerType,
            request.OwnerId,
            request.Search,
            request.Page ?? 1,
            request.PageSize ?? 50,
            cancellationToken);

        return new ListAccountingBooksResponse(
            MapItems(items),
            totalCount,
            request.Page ?? 1,
            request.PageSize ?? 50);
    }

    static IReadOnlyList<AccountingBookItem> MapItems(IReadOnlyList<AccountingBookSqlModel> items)
    {
        return items.Select(x => new AccountingBookItem(
            x.Id,
            x.Code,
            x.Title,
            x.OwnerType,
            x.OwnerId,
            x.Status,
            x.CreatedAt)).ToList();
    }
}
