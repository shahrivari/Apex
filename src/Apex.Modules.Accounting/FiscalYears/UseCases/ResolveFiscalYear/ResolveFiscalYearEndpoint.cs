using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;

public static class ResolveFiscalYearEndpoint
{
    public static RouteGroupBuilder MapResolveFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/resolve", async ([AsParameters] ResolveFiscalYearRequest request,
                [FromServices] ResolveFiscalYearHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("ResolveFiscalYear");
        return group;
    }
}
