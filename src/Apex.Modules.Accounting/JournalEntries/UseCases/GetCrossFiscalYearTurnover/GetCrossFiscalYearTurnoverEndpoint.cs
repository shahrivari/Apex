using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetCrossFiscalYearTurnover;

public static class GetCrossFiscalYearTurnoverEndpoint
{
    public static RouteGroupBuilder MapGetCrossFiscalYearTurnoverEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/reports/cross-fiscal-year-turnover", async (
                [FromBody] GetCrossFiscalYearTurnoverRequest request,
                [FromServices] GetCrossFiscalYearTurnoverHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("GetCrossFiscalYearTurnover");
        return group;
    }
}
