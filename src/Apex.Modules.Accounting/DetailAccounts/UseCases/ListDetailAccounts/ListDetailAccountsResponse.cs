namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;

public sealed record ListDetailAccountsResponse(
    IReadOnlyList<DetailAccountItem> Items,
    int TotalCount,
    int Page,
    int PageSize
);
