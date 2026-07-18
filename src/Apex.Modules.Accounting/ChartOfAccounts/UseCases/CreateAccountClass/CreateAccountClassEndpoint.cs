using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;

internal static class CreateAccountClassEndpoint
{
    internal static RouteGroupBuilder MapCreateAccountClassEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/classes", async ([FromBody] CreateAccountClassRequest request,
                [FromServices] CreateAccountClassHandler handler, CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return Results.Created($"/api/v1/accounting/chart-of-accounts/classes/{response.Id}", response);
            })
            .WithName("CreateAccountClass");
        return group;
    }
}
