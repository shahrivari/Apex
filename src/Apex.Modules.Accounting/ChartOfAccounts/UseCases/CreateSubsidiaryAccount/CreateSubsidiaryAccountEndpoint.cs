using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;

internal static class CreateSubsidiaryAccountEndpoint
{
    internal static RouteGroupBuilder MapCreateSubsidiaryAccountEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/subsidiary-accounts", async ([FromBody] CreateSubsidiaryAccountRequest request,
                [FromServices] CreateSubsidiaryAccountHandler handler, CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return Results.Created($"/api/v1/accounting/chart-of-accounts/subsidiary-accounts/{response.Id}", response);
            })
            .WithName("CreateSubsidiaryAccount");
        return group;
    }
}
