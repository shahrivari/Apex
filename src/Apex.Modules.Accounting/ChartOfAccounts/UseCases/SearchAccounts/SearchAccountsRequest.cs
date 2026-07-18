using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;

internal sealed record SearchAccountsRequest(
    AccountLevel? Level, long? ParentId, string? Term, AccountNature? Nature, DetailAccountType? DetailAccountType,
    AccountStatus? Status, int Page = 1, int PageSize = 50);
