using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.DeleteDraftJournalEntry;

public static class DeleteDraftJournalEntryEndpoint
{
    public static RouteGroupBuilder MapDeleteDraftJournalEntryEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{fiscalYearId:long}/{id:long}", async (long fiscalYearId, long id,
                [FromServices] DeleteDraftJournalEntryHandler handler, CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(fiscalYearId, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteDraftJournalEntry")
            .RequireAuthorization();
        return group;
    }
}
