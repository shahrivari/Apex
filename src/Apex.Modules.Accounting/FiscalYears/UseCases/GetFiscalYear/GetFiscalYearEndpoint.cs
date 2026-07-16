using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;

public static class GetFiscalYearEndpoint
{
    public static RouteGroupBuilder MapGetFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:long}", async (long id, [FromServices] GetFiscalYearHandler handler,
                CancellationToken cancellationToken) => Results.Ok(await handler.HandleAsync(id, cancellationToken)))
            .WithName("GetFiscalYear");
        return group;
    }
}
