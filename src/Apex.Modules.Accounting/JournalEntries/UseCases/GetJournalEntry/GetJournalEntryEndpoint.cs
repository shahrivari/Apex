using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntry;

public static class GetJournalEntryEndpoint
{
    public static RouteGroupBuilder MapGetJournalEntryEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{fiscalYearId:long}/{id:long}", async (long fiscalYearId, long id,
                [FromServices] GetJournalEntryHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.GetByIdAsync(fiscalYearId, id, cancellationToken)))
            .WithName("GetJournalEntry")
            .RequireAuthorization();

        group.MapGet("/by-reference", async (
                [FromQuery] long accountingBookId, [FromQuery] long fiscalYearId,
                [FromQuery] long referenceNumber, [FromServices] GetJournalEntryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.GetByReferenceNumberAsync(
                accountingBookId, fiscalYearId, referenceNumber, cancellationToken)))
            .WithName("GetJournalEntryByReferenceNumber")
            .RequireAuthorization();

        group.MapGet("/by-number", async (
                [FromQuery] long accountingBookId, [FromQuery] long fiscalYearId,
                [FromQuery] long journalEntryNumber, [FromServices] GetJournalEntryHandler handler,
                CancellationToken cancellationToken) =>
            Results.Ok(await handler.GetByJournalEntryNumberAsync(
                accountingBookId, fiscalYearId, journalEntryNumber, cancellationToken)))
            .WithName("GetJournalEntryByNumber")
            .RequireAuthorization();

        return group;
    }
}
