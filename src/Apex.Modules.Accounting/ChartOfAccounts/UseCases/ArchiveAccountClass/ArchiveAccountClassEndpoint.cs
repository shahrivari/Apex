using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveAccountClass;

internal static class ArchiveAccountClassEndpoint
{
    internal static RouteGroupBuilder MapArchiveAccountClassEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/classes/{id:long}/archive", async (long id,
                [FromServices] ArchiveAccountClassHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("ArchiveAccountClass");
        return group;
    }
}
