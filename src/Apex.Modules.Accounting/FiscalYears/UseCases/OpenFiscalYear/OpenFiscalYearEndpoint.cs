using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;

public static class OpenFiscalYearEndpoint
{
    public static RouteGroupBuilder MapOpenFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/open", async (long id, [FromServices] OpenFiscalYearHandler handler,
                CancellationToken cancellationToken) => Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("OpenFiscalYear");
        return group;
    }
}
