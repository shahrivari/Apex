using Apex.Modules.Accounting.AccountingBooks;

namespace Apex.Modules.Accounting.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class AccountingEndpoints
{
    public static IEndpointRouteBuilder MapAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/api/accounting")
            .WithTags("Accounting");

        group.MapGet("/info", () => Results.Ok(new
        {
            Module = AccountingModule.Name,
            Status = "Ready"
        }))
        .AllowAnonymous();

        app.MapAccountingBookEndpoints();

        return app;
    }
}
