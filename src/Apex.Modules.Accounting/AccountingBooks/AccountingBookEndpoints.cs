using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;
using Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks;

public static class AccountingBookEndpoints
{
    public static IEndpointRouteBuilder MapAccountingBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting/books")
            .WithTags("Accounting - Books");

        group.MapPost("", async (
                [FromBody] CreateAccountingBookRequest request,
                [FromServices] CreateAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken);
                return Results.Created($"/api/v1/accounting/books/{response.Id}", response);
            })
            .WithName("CreateAccountingBook");

        group.MapGet("/{id:long}", async (
                long id,
                [FromServices] GetAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetAccountingBook");

        group.MapGet("", async (
                [AsParameters] ListAccountingBooksRequest request,
                [FromServices] ListAccountingBooksHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListAccountingBooks");

        group.MapPost("/{id:long}/activate", async (
                long id,
                [FromServices] ActivateAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ActivateAccountingBook");

        group.MapPost("/{id:long}/suspend", async (
                long id,
                [FromServices] SuspendAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("SuspendAccountingBook");

        group.MapPost("/{id:long}/archive", async (
                long id,
                [FromServices] ArchiveAccountingBookHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(id, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ArchiveAccountingBook");

        return app;
    }
}