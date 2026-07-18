using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateAccountClass;

internal static class UpdateAccountClassEndpoint
{
    internal static RouteGroupBuilder MapUpdateAccountClassEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/classes/{id:long}", async (long id, [FromBody] UpdateAccountClassRequest request,
                [FromServices] UpdateAccountClassHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateAccountClass");
        return group;
    }
}
