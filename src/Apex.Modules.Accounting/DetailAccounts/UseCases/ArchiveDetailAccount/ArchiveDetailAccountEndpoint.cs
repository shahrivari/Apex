using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ArchiveDetailAccount;

public static class ArchiveDetailAccountEndpoint
{
    public static RouteGroupBuilder MapArchiveDetailAccountEndpoint(this RouteGroupBuilder g)
    {
        g.MapPost(
                "/{id:long}/archive",
                async (
                    long id,
                    [FromServices] ArchiveDetailAccountHandler h,
                    CancellationToken ct
                ) =>
                {
                    await h.HandleAsync(id, ct);
                    return Results.NoContent();
                }
            )
            .WithName("ArchiveDetailAccount")
            .RequireAuthorization();
        return g;
    }
}
