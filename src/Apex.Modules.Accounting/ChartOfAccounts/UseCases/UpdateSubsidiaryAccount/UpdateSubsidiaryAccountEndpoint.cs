using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;

internal static class UpdateSubsidiaryAccountEndpoint
{
    internal static RouteGroupBuilder MapUpdateSubsidiaryAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/subsidiary-accounts/{id:long}", async (long id, [FromBody] UpdateSubsidiaryAccountRequest request,
                [FromServices] UpdateSubsidiaryAccountHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(id, request, ct)))
            .WithName("UpdateSubsidiaryAccount");
        return group;
    }
}
