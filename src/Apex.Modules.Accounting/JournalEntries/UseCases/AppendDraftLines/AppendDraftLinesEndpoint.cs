using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;

public static class AppendDraftLinesEndpoint
{
    public static RouteGroupBuilder MapAppendDraftLinesEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{fiscalYearId:long}/{id:long}/lines", async (long fiscalYearId, long id,
                [FromBody] AppendDraftLinesRequest request,
                [FromServices] AppendDraftLinesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(fiscalYearId, id, request, cancellationToken)))
            .WithName("AppendDraftJournalEntryLines")
            .RequireAuthorization();
        return group;
    }
}
