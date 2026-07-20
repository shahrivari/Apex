using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears.UseCases.RepairFiscalYearDirectory;

public static class RepairFiscalYearDirectoryEndpoint
{
    public static RouteGroupBuilder MapRepairFiscalYearDirectoryEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/repair-directory-index", async (
                long id,
                [FromServices] RepairFiscalYearDirectoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RepairFiscalYearDirectory");
        return group;
    }
}
