using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;

public static class CancelFiscalYearEndpoint
{
    public static RouteGroupBuilder MapCancelFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/cancel", async (long id, [FromBody] CancelFiscalYearRequest request,
                [FromServices] CancelFiscalYearHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("CancelFiscalYear");
        return group;
    }
}
