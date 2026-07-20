using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;

public static class SearchJournalEntriesEndpoint
{
    public static RouteGroupBuilder MapSearchJournalEntriesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", async ([AsParameters] SearchJournalEntriesRequest request,
                [FromServices] SearchJournalEntriesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("SearchJournalEntries")
            .RequireAuthorization();
        return group;
    }
}
