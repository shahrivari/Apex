using Apex.Modules.Accounting.JournalEntries.UseCases.AppendDraftLines;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.DeleteDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.PostJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReplaceDraftLines;
using Apex.Modules.Accounting.JournalEntries.UseCases.ReverseJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.SearchJournalEntries;
using Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Apex.Modules.Accounting.JournalEntries;

public static class JournalEntryEndpoints
{
    public static IEndpointRouteBuilder MapJournalEntryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounting/journal-entries")
            .WithTags("Accounting - Journal Entries")
            .RequireAuthorization();

        group.MapCreateDraftJournalEntryEndpoint();
        group.MapSearchJournalEntriesEndpoint();
        group.MapGetJournalEntryEndpoint();
        group.MapUpdateDraftJournalEntryEndpoint();
        group.MapAppendDraftLinesEndpoint();
        group.MapReplaceDraftLinesEndpoint();
        group.MapPostJournalEntryEndpoint();
        group.MapReverseJournalEntryEndpoint();
        group.MapDeleteDraftJournalEntryEndpoint();
        return app;
    }
}
