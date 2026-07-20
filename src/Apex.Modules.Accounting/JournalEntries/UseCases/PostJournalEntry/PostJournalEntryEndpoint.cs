using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.PostJournalEntry;

public static class PostJournalEntryEndpoint
{
    public static RouteGroupBuilder MapPostJournalEntryEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{fiscalYearId:long}/{id:long}/post", async (long fiscalYearId, long id,
                [FromServices] PostJournalEntryHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(fiscalYearId, id, cancellationToken)))
            .WithName("PostJournalEntry")
            .RequireAuthorization();
        return group;
    }
}
