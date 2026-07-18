namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;

public sealed record ListDetailAccountsRequest(
    string? Type,
    string? Status,
    string? Search,
    int Page = 1,
    int PageSize = 50
);
