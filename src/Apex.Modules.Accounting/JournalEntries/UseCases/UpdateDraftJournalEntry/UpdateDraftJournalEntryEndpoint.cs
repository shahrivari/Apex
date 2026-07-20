using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;

public static class UpdateDraftJournalEntryEndpoint
{
    public static RouteGroupBuilder MapUpdateDraftJournalEntryEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{fiscalYearId:long}/{id:long}", async (long fiscalYearId, long id,
                [FromBody] UpdateDraftJournalEntryRequest request,
                [FromServices] UpdateDraftJournalEntryHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(fiscalYearId, id, request, cancellationToken)))
            .WithName("UpdateDraftJournalEntry")
            .RequireAuthorization();
        return group;
    }
}
