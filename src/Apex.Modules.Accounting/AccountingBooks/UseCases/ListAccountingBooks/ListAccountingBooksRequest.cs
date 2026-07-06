namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public sealed class ListAccountingBooksRequest
{
    public string? Status { get; init; }

    public string? OwnerType { get; init; }

    public string? OwnerId { get; init; }

    public string? Search { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}
