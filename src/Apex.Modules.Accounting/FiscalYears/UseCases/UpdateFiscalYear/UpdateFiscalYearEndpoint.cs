using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;

public static class UpdateFiscalYearEndpoint
{
    public static RouteGroupBuilder MapUpdateFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:long}", async (long id, [FromBody] UpdateFiscalYearRequest request,
                [FromServices] UpdateFiscalYearHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)))
            .WithName("UpdateFiscalYear");
        return group;
    }
}
