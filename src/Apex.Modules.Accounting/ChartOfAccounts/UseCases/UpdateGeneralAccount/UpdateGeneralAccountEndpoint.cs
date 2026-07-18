using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;

internal static class UpdateGeneralAccountEndpoint
{
    internal static RouteGroupBuilder MapUpdateGeneralAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/general-accounts/{id:long}", async (long id, [FromBody] UpdateGeneralAccountRequest request,
                [FromServices] UpdateGeneralAccountHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateGeneralAccount");
        return group;
    }
}
