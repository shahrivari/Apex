using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.DeleteFiscalYear;

public static class DeleteFiscalYearEndpoint
{
    public static RouteGroupBuilder MapDeleteFiscalYearEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:long}", async (long id, [FromServices] DeleteFiscalYearHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteFiscalYear");
        return group;
    }
}
