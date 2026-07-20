using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;

public static class GetTransactionReportEndpoint
{
    public static RouteGroupBuilder MapGetTransactionReportEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/reports/general-ledger", HandleAsync)
            .WithName("GetGeneralLedgerReport");
        group.MapPost("/reports/journal", HandleAsync)
            .WithName("GetJournalReport");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] GetTransactionReportRequest request,
        [FromServices] GetTransactionReportHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(request, cancellationToken));
}
