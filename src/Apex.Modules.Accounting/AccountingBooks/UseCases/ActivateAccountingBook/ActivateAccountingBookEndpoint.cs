using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;

public static class ActivateAccountingBookEndpoint
{
    public static RouteGroupBuilder MapActivateAccountingBookEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/activate", async (
                long id,
                [FromServices] ActivateAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ActivateAccountingBook");

        return group;
    }
}
