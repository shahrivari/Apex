using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;

public static class ArchiveAccountingBookEndpoint
{
    public static RouteGroupBuilder MapArchiveAccountingBookEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:long}/archive", async (
                long id,
                [FromServices] ArchiveAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ArchiveAccountingBook");

        return group;
    }
}
