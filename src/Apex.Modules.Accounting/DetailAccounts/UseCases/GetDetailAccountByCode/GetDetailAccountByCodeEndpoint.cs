using Microsoft.AspNetCore.Mvc;

namespace Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccountByCode;

public static class GetDetailAccountByCodeEndpoint
{
    public static RouteGroupBuilder MapGetDetailAccountByCodeEndpoint(this RouteGroupBuilder g)
    {
        g.MapGet(
                "/by-code/{code}",
                async (
                    string code,
                    [FromServices] GetDetailAccountByCodeHandler h,
                    CancellationToken ct
                ) => Results.Ok(await h.HandleAsync(code, ct))
            )
            .WithName("GetDetailAccountByCode")
            .RequireAuthorization();
        return g;
    }
}
