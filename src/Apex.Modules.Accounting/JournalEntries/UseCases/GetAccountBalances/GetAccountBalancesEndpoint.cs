using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetAccountBalances;

public static class GetAccountBalancesEndpoint
{
    public static RouteGroupBuilder MapGetAccountBalancesEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/reports/balances", async (
                [FromBody] GetAccountBalancesRequest request,
                [FromServices] GetAccountBalancesHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("GetJournalEntryAccountBalances");
        return group;
    }
}
