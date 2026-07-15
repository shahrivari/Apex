using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;

public static class SuspendAccountingBookEndpoint
{
    public static RouteGroupBuilder MapSuspendAccountingBookEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/suspend", async (
                long id,
                [FromServices] SuspendAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("SuspendAccountingBook");

        return group;
    }
}
