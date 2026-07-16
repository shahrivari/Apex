using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;

public static class ListFiscalYearsEndpoint
{
    public static RouteGroupBuilder MapListFiscalYearsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", async ([AsParameters] ListFiscalYearsRequest request,
                [FromServices] ListFiscalYearsHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(request, cancellationToken)))
            .WithName("ListFiscalYears");
        return group;
    }
}
