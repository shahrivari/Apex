using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateAccountClass;

internal static class ReactivateAccountClassEndpoint
{
    internal static RouteGroupBuilder MapReactivateAccountClassEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/classes/{id:long}/reactivate", async (long id,
                [FromServices] ReactivateAccountClassHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("ReactivateAccountClass");
        return group;
    }
}
