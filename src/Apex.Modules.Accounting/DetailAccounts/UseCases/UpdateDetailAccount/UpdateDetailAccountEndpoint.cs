using Microsoft.AspNetCore.Mvc;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;

public static class UpdateDetailAccountEndpoint
{
    public static RouteGroupBuilder MapUpdateDetailAccountEndpoint(this RouteGroupBuilder g)
    {
        g.MapPut(
                "/{id:long}",
                async (
                    long id,
                    [FromBody] UpdateDetailAccountRequest r,
                    [FromServices] UpdateDetailAccountHandler h,
                    CancellationToken ct
                ) => Results.Ok(await h.HandleAsync(id, r, ct))
            )
            .WithName("UpdateDetailAccount")
            .RequireAuthorization();
        return g;
    }
}
