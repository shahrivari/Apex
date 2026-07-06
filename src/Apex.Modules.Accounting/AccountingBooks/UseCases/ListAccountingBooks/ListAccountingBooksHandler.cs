using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.AccountingBooks.SqlModels;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public sealed class ListAccountingBooksHandler
{
    private readonly AccountingBookReadRepository _readRepository;

    public ListAccountingBooksHandler(AccountingBookReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ListAccountingBooksResponse> HandleAsync(
        ListAccountingBooksRequest request,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _readRepository.ListAsync(
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
