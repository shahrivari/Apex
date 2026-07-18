namespace Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;

public sealed record SearchDetailAccountsForPostingResponse(
    IReadOnlyList<PostingDetailAccountItem> Items
);
