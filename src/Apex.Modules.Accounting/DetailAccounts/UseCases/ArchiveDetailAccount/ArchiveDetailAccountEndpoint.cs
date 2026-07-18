using Microsoft.AspNetCore.Mvc;

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
