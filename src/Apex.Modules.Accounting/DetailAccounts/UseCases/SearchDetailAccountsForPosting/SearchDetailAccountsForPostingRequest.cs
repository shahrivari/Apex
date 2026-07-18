namespace Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;

public sealed record SearchDetailAccountsForPostingRequest(
    string Type,
    string? Search,
    int Limit = 20
);
