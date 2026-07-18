using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveSubsidiaryAccount;

internal static class ArchiveSubsidiaryAccountEndpoint
{
    internal static RouteGroupBuilder MapArchiveSubsidiaryAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/subsidiary-accounts/{id:long}/archive", async (long id,
                [FromServices] ArchiveSubsidiaryAccountHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("ArchiveSubsidiaryAccount");
        return group;
    }
}
