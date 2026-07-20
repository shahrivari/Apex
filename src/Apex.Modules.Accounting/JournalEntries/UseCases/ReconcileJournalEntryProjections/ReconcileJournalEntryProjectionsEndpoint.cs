using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.ReconcileJournalEntryProjections;

public static class ReconcileJournalEntryProjectionsEndpoint
{
    public static RouteGroupBuilder MapReconcileJournalEntryProjectionsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{fiscalYearId:long}/projections/reconcile", async (
                long fiscalYearId,
                [FromServices] ReconcileJournalEntryProjectionsHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(fiscalYearId, cancellationToken)))
            .WithName("ReconcileJournalEntryProjections");
        return group;
    }
}
