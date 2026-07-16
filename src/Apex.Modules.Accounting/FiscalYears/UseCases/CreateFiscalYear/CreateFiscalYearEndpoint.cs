using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;

public static class CreateFiscalYearEndpoint
{
    public static RouteGroupBuilder MapCreateFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("", async ([FromBody] CreateFiscalYearRequest request,
                [FromServices] CreateFiscalYearHandler handler, CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken);
                return Results.Created($"/api/v1/accounting/fiscal-years/{response.Id}", response);
            })
            .WithName("CreateFiscalYear");
        return group;
    }
}
