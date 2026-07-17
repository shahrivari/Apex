using Apex.Modules.Accounting.AccountingBooks;
using Apex.Modules.Accounting.FiscalYears;
using Apex.Modules.Accounting.ChartOfAccounts;

namespace Apex.Modules.Accounting.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class AccountingEndpoints
{
    public static IEndpointRouteBuilder MapAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting")
            .WithTags("Accounting");

        group.MapGet("/info", () => Results.Ok(new
        {
            Module = AccountingModule.Name,
            Status = "Ready"
        }))
        .AllowAnonymous();

        app.MapAccountingBookEndpoints();
        app.MapFiscalYearEndpoints();
        app.MapChartOfAccountsEndpoints();

        return app;
    }
}
