using Apex.Modules.Accounting.AccountingBooks.UseCases.ActivateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ArchiveAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.CreateAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.GetAccountingBook;
using Apex.Modules.Accounting.AccountingBooks.UseCases.ListAccountingBooks;
using Apex.Modules.Accounting.AccountingBooks.UseCases.SuspendAccountingBook;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.AccountingBooks;

public static class AccountingBookEndpoints
{
    public static IEndpointRouteBuilder MapAccountingBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting/books")
            .WithTags("Accounting - Books")
            .RequireAuthorization();

        group.MapCreateAccountingBookEndpoint();
        group.MapGetAccountingBookEndpoint();
        group.MapListAccountingBooksEndpoint();
        group.MapActivateAccountingBookEndpoint();
        group.MapSuspendAccountingBookEndpoint();
        group.MapArchiveAccountingBookEndpoint();

        return app;
    }
}
