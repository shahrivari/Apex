using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;

public static class CreateDraftJournalEntryEndpoint
{
    public static RouteGroupBuilder MapCreateDraftJournalEntryEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("", async ([FromBody] CreateDraftJournalEntryRequest request,
                [FromServices] CreateDraftJournalEntryHandler handler, CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken);
                return Results.Created(
                    $"/api/v1/accounting/journal-entries/{response.FiscalYearId}/{response.Id}", response);
            })
            .WithName("CreateDraftJournalEntry")
            .RequireAuthorization();
        return group;
    }
}
