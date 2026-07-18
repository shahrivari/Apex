using Microsoft.AspNetCore.Mvc;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;

public static class CreateDetailAccountEndpoint
{
    public static RouteGroupBuilder MapCreateDetailAccountEndpoint(this RouteGroupBuilder g)
    {
        g.MapPost(
                "",
                async (
                    [FromBody] CreateDetailAccountRequest r,
                    [FromServices] CreateDetailAccountHandler h,
                    CancellationToken ct
                ) =>
                {
                    var x = await h.HandleAsync(r, ct);
                    return Results.Created($"/api/v1/accounting/detail-accounts/{x.Id}", x);
                }
            )
            .WithName("CreateDetailAccount")
            .RequireAuthorization();
        return g;
    }
}
