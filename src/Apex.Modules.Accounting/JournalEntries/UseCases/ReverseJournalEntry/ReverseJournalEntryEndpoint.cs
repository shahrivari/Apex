using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;

public static class ReverseJournalEntryEndpoint
{
    public static RouteGroupBuilder MapReverseJournalEntryEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{fiscalYearId:long}/by-reference/{referenceNumber:long}/reverse", async (
                long fiscalYearId,
                long referenceNumber,
                [FromBody] ReverseJournalEntryRequest request,
                [FromServices] ReverseJournalEntryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(
                fiscalYearId, referenceNumber, request, cancellationToken)))
            .WithName("ReverseJournalEntry");
        return group;
    }
}
