using Microsoft.AspNetCore.Mvc;

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
