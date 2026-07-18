using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;

internal static class CreateGeneralAccountEndpoint
{
    internal static RouteGroupBuilder MapCreateGeneralAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/general-accounts", async ([FromBody] CreateGeneralAccountRequest request,
                [FromServices] CreateGeneralAccountHandler handler, CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return Results.Created($"/api/v1/accounting/chart-of-accounts/general-accounts/{response.Id}", response);
            })
            .WithName("CreateGeneralAccount");
        return group;
    }
}
