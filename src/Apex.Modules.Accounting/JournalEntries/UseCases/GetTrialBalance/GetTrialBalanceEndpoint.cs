using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTrialBalance;

public static class GetTrialBalanceEndpoint
{
    public static RouteGroupBuilder MapGetTrialBalanceEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/reports/trial-balance", async (
                [FromBody] GetTrialBalanceRequest request,
                [FromServices] GetTrialBalanceHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("GetJournalEntryTrialBalance");
        return group;
    }
}
