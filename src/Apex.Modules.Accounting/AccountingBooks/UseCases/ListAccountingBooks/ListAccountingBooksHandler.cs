using Apex.Modules.Accounting.AccountingBooks.Repositories;
using Apex.Modules.Accounting.AccountingBooks.SqlModels;
using FluentValidation;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public sealed class ListAccountingBooksHandler
{
    private readonly AccountingBookReadRepository _readRepository;
    private readonly IValidator<ListAccountingBooksRequest> _validator;

    public ListAccountingBooksHandler(
        AccountingBookReadRepository readRepository,
        IValidator<ListAccountingBooksRequest> validator)
    {
        _readRepository = readRepository;
        _validator = validator;
    }

    public async Task<ListAccountingBooksResponse> HandleAsync(
        ListAccountingBooksRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

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
