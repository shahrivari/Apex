using Apex.Modules.Accounting.DetailAccounts.UseCases.ArchiveDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.DeleteDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccountByCode;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;
using Apex.Modules.Accounting.DetailAccounts.UseCases.ReactivateDetailAccount;
using Apex.Modules.Accounting.DetailAccounts.UseCases.SearchDetailAccountsForPosting;
using Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.DetailAccounts;

public static class DetailAccountEndpoints
{
    public static IEndpointRouteBuilder MapDetailAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/accounting/detail-accounts")
            .WithTags("Accounting - Detail Accounts")
            .RequireAuthorization();
        g.MapCreateDetailAccountEndpoint();
        g.MapUpdateDetailAccountEndpoint();
        g.MapGetDetailAccountEndpoint();
        g.MapGetDetailAccountByCodeEndpoint();
        g.MapListDetailAccountsEndpoint();
        g.MapSearchDetailAccountsForPostingEndpoint();
        g.MapArchiveDetailAccountEndpoint();
        g.MapReactivateDetailAccountEndpoint();
        g.MapDeleteDetailAccountEndpoint();
        return app;
    }
}
