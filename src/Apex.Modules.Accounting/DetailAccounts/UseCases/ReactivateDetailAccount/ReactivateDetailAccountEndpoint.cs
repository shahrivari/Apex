using Microsoft.AspNetCore.Mvc;

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
