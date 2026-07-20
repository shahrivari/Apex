using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReplaceDraftLines;

public static class ReplaceDraftLinesEndpoint
{
    public static RouteGroupBuilder MapReplaceDraftLinesEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{fiscalYearId:long}/{id:long}/lines", async (long fiscalYearId, long id,
                [FromBody] ReplaceDraftLinesRequest request,
                [FromServices] ReplaceDraftLinesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(fiscalYearId, id, request, cancellationToken)))
            .WithName("ReplaceDraftJournalEntryLines")
            .RequireAuthorization();
        return group;
    }
}
