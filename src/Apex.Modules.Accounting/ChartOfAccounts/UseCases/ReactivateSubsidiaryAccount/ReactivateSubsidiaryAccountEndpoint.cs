using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateSubsidiaryAccount;

internal static class ReactivateSubsidiaryAccountEndpoint
{
    internal static RouteGroupBuilder MapReactivateSubsidiaryAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/subsidiary-accounts/{id:long}/reactivate", async (long id,
                [FromServices] ReactivateSubsidiaryAccountHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("ReactivateSubsidiaryAccount");
        return group;
    }
}
