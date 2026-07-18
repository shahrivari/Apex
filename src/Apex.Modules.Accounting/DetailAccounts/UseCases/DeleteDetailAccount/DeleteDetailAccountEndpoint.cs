using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.DeleteDetailAccount;

public static class DeleteDetailAccountEndpoint
{
    public static RouteGroupBuilder MapDeleteDetailAccountEndpoint(this RouteGroupBuilder g)
    {
        g.MapDelete(
                "/{id:long}",
                async (
                    long id,
                    [FromServices] DeleteDetailAccountHandler h,
                    CancellationToken ct
                ) =>
                {
                    await h.HandleAsync(id, ct);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteUnusedDetailAccount")
            .RequireAuthorization();
        return g;
    }
}
