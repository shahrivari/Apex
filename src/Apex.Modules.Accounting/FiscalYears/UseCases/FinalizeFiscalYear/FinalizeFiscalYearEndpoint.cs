using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;

public static class FinalizeFiscalYearEndpoint
{
    public static RouteGroupBuilder MapFinalizeFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/finalize", async (
                long id,
                [FromBody] FinalizeFiscalYearRequest request,
                [FromServices] FinalizeFiscalYearHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("FinalizeFiscalYear");
        return group;
    }
}
