namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;

internal sealed record SearchAccountItem(
    long Id, string Level, long? ParentId, string Code, string Name, string? Nature, string? DetailAccountType,
    string Status);

internal sealed record SearchAccountsResponse(IReadOnlyList<SearchAccountItem> Items, int Page, int PageSize);
