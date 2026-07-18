using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateGeneralAccount;

internal static class ReactivateGeneralAccountEndpoint
{
    internal static RouteGroupBuilder MapReactivateGeneralAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/general-accounts/{id:long}/reactivate", async (long id,
                [FromServices] ReactivateGeneralAccountHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("ReactivateGeneralAccount");
        return group;
    }
}
