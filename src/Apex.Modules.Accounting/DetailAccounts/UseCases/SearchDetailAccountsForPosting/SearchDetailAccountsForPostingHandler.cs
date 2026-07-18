using Apex.Modules.Accounting.DetailAccounts.Domain;
using Apex.Modules.Accounting.DetailAccounts.Repositories;
using FluentValidation;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;

public sealed class SearchDetailAccountsForPostingHandler(
    IDetailAccountReadRepository repo,
    IValidator<SearchDetailAccountsForPostingRequest> validator
)
{
    public async Task<SearchDetailAccountsForPostingResponse> HandleAsync(
        SearchDetailAccountsForPostingRequest r,
        CancellationToken ct
    )
    {
        await validator.ValidateAndThrowAsync(r, ct);
        var rows = await repo.SearchForPostingAsync(
            DetailAccountValues.ParseType(r.Type).ToDatabaseValue(),
            r.Search,
            r.Limit,
            ct
        );
        return new(rows.Select(x => new PostingDetailAccountItem(x.Code, x.Name, x.Type)).ToList());
    }
}
