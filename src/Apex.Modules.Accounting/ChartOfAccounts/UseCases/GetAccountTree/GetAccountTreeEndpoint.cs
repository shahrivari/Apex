using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccountTree;

internal static class GetAccountTreeEndpoint
{
    internal static RouteGroupBuilder MapGetAccountTreeEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/tree", async ([FromQuery] bool? includeArchived,
                [FromServices] GetAccountTreeHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(includeArchived ?? false, ct)))
            .WithName("GetAccountTree");
        return group;
    }
}
