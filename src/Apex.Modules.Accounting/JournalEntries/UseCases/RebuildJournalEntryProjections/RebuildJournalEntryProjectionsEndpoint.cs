using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.RebuildJournalEntryProjections;

public static class RebuildJournalEntryProjectionsEndpoint
{
    public static RouteGroupBuilder MapRebuildJournalEntryProjectionsEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{fiscalYearId:long}/projections/rebuild", async (
                long fiscalYearId,
                [FromBody] RebuildJournalEntryProjectionsRequest request,
                [FromServices] RebuildJournalEntryProjectionsHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(fiscalYearId, request, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RebuildJournalEntryProjections");
        return group;
    }
}
