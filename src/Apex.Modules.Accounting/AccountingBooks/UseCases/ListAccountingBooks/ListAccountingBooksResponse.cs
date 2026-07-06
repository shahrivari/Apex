namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public sealed class ListAccountingBooksResponse
{
    public IReadOnlyList<AccountingBookItem> Items { get; init; } = Array.Empty<AccountingBookItem>();

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public ListAccountingBooksResponse(IReadOnlyList<AccountingBookItem> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}

public sealed class AccountingBookItem
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Title { get; init; } = null!;

    public string OwnerType { get; init; } = null!;

    public string OwnerId { get; init; } = null!;

    public string Status { get; init; } = null!;

    public DateTime CreatedAt { get; init; }

    public AccountingBookItem(long id, string code, string title, string ownerType, string ownerId, string status, DateTime createdAt)
    {
        Id = id;
        Code = code;
        Title = title;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Status = status;
        CreatedAt = createdAt;
    }
}
