using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;

public static class SearchDetailAccountsForPostingEndpoint
{
    public static RouteGroupBuilder MapSearchDetailAccountsForPostingEndpoint(
        this RouteGroupBuilder g
    )
    {
        g.MapGet(
                "/posting-search",
                async (
                    [AsParameters] SearchDetailAccountsForPostingRequest r,
                    [FromServices] SearchDetailAccountsForPostingHandler h,
                    CancellationToken ct
                ) => Results.Ok(await h.HandleAsync(r, ct))
            )
            .WithName("SearchDetailAccountsForPosting")
            .RequireAuthorization();
        return g;
    }
}
