using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveGeneralAccount;

internal static class ArchiveGeneralAccountEndpoint
{
    internal static RouteGroupBuilder MapArchiveGeneralAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/general-accounts/{id:long}/archive", async (long id,
                [FromServices] ArchiveGeneralAccountHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("ArchiveGeneralAccount");
        return group;
    }
}
