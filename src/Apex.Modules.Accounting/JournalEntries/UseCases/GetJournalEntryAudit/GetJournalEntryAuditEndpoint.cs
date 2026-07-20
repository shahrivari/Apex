using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntryAudit;

public static class GetJournalEntryAuditEndpoint
{
    public static RouteGroupBuilder MapGetJournalEntryAuditEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{fiscalYearId:long}/by-reference/{referenceNumber:long}/audit", async (
                long fiscalYearId,
                long referenceNumber,
                [FromQuery] long accountingBookId,
                [FromServices] GetJournalEntryAuditHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(
                new GetJournalEntryAuditRequest
                {
                    AccountingBookId = accountingBookId,
                    FiscalYearId = fiscalYearId,
                    ReferenceNumber = referenceNumber
                }, cancellationToken)))
            .WithName("GetJournalEntryAudit");
        return group;
    }
}
