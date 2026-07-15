using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;

public static class GetAccountingBookEndpoint
{
    public static RouteGroupBuilder MapGetAccountingBookEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:long}", async (
                long id,
                [FromServices] GetAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetAccountingBook");

        return group;
    }
}
