using Microsoft.AspNetCore.Mvc;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;

public static class GetDetailAccountEndpoint
{
    public static RouteGroupBuilder MapGetDetailAccountEndpoint(this RouteGroupBuilder g)
    {
        g.MapGet(
                "/{id:long}",
                async (long id, [FromServices] GetDetailAccountHandler h, CancellationToken ct) =>
                    Results.Ok(await h.HandleAsync(id, ct))
            )
            .WithName("GetDetailAccount")
            .RequireAuthorization();
        return g;
    }
}
