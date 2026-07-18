using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;

internal static class SearchAccountsEndpoint
{
    internal static RouteGroupBuilder MapSearchAccountsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", async ([AsParameters] SearchAccountsRequest request,
                [FromServices] SearchAccountsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(request, ct)))
            .WithName("SearchAccounts");
        return group;
    }
}
