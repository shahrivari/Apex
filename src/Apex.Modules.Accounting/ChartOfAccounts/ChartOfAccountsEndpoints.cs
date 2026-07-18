using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ArchiveSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccountTree;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.ReactivateSubsidiaryAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.SearchAccounts;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateAccountClass;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;
using Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.ChartOfAccounts;

internal static class ChartOfAccountsEndpoints
{
    internal static IEndpointRouteBuilder MapChartOfAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting/chart-of-accounts")
            .WithTags("Accounting - Chart of Accounts")
            .RequireAuthorization();

        group.MapCreateAccountClassEndpoint();
        group.MapUpdateAccountClassEndpoint();
        group.MapArchiveAccountClassEndpoint();
        group.MapReactivateAccountClassEndpoint();
        group.MapCreateGeneralAccountEndpoint();
        group.MapUpdateGeneralAccountEndpoint();
        group.MapArchiveGeneralAccountEndpoint();
        group.MapReactivateGeneralAccountEndpoint();
        group.MapCreateSubsidiaryAccountEndpoint();
        group.MapUpdateSubsidiaryAccountEndpoint();
        group.MapArchiveSubsidiaryAccountEndpoint();
        group.MapReactivateSubsidiaryAccountEndpoint();
        group.MapGetAccountEndpoint();
        group.MapGetAccountTreeEndpoint();
        group.MapSearchAccountsEndpoint();
        return app;
    }
}
