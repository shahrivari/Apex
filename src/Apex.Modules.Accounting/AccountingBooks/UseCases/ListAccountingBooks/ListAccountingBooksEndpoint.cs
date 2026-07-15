using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;

public static class ListAccountingBooksEndpoint
{
    public static RouteGroupBuilder MapListAccountingBooksEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", async (
                [AsParameters] ListAccountingBooksRequest request,
                [FromServices] ListAccountingBooksHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListAccountingBooks");

        return group;
    }
}
