using Apex.Modules.Accounting.FiscalYears.UseCases.CancelFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.CreateFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.DeleteFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.GetFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.ListFiscalYears;
using Apex.Modules.Accounting.FiscalYears.UseCases.OpenFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.ResolveFiscalYear;
using Apex.Modules.Accounting.FiscalYears.UseCases.RepairFiscalYearDirectory;
using Apex.Modules.Accounting.FiscalYears.UseCases.UpdateFiscalYear;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.FiscalYears;

public static class FiscalYearEndpoints
{
    public static IEndpointRouteBuilder MapFiscalYearEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting/fiscal-years")
            .WithTags("Accounting - Fiscal Years")
            .RequireAuthorization();

        group.MapCreateFiscalYearEndpoint();
        group.MapGetFiscalYearEndpoint();
        group.MapListFiscalYearsEndpoint();
        group.MapResolveFiscalYearEndpoint();
        group.MapUpdateFiscalYearEndpoint();
        group.MapDeleteFiscalYearEndpoint();
        group.MapOpenFiscalYearEndpoint();
        group.MapFinalizeFiscalYearEndpoint();
        group.MapCancelFiscalYearEndpoint();
        group.MapRepairFiscalYearDirectoryEndpoint();
        return app;
    }
}
