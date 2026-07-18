using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccount;

internal static class GetAccountEndpoint
{
    internal static RouteGroupBuilder MapGetAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{level}/{id:long}", async (AccountLevel level, long id,
                [FromServices] GetAccountHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(level, id, ct)))
            .WithName("GetChartAccount");
        return group;
    }
}
