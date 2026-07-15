using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;

public static class CreateAccountingBookEndpoint
{
    public static RouteGroupBuilder MapCreateAccountingBookEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("", async (
                [FromBody] CreateAccountingBookRequest request,
                [FromServices] CreateAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken);
                return Results.Created($"/api/v1/accounting/books/{response.Id}", response);
            })
            .WithName("CreateAccountingBook");

        return group;
    }
}
