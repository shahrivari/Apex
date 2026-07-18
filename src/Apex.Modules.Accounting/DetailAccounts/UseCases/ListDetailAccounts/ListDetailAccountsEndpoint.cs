using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;

public static class ListDetailAccountsEndpoint
{
    public static RouteGroupBuilder MapListDetailAccountsEndpoint(this RouteGroupBuilder g)
    {
        g.MapGet(
                "",
                async (
                    [AsParameters] ListDetailAccountsRequest r,
                    [FromServices] ListDetailAccountsHandler h,
                    CancellationToken ct
                ) => Results.Ok(await h.HandleAsync(r, ct))
            )
            .WithName("ListDetailAccounts")
            .RequireAuthorization();
        return g;
    }
}
