using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ReactivateDetailAccount;

public static class ReactivateDetailAccountEndpoint
{
    public static RouteGroupBuilder MapReactivateDetailAccountEndpoint(this RouteGroupBuilder g)
    {
        g.MapPost(
                "/{id:long}/reactivate",
                async (
                    long id,
                    [FromServices] ReactivateDetailAccountHandler h,
                    CancellationToken ct
                ) =>
                {
                    await h.HandleAsync(id, ct);
                    return Results.NoContent();
                }
            )
            .WithName("ReactivateDetailAccount")
            .RequireAuthorization();
        return g;
    }
}
