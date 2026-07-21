using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group I ("Numbering and daily finalization", NUM-001
/// through NUM-018 — spec §9.I; NUM-019 is concurrency and is implemented in Phase 5's
/// <c>ConcurrencyScenarios.cs</c>).
///
/// Every rule here was confirmed by reading <c>FiscalYearErrors</c>/<c>JournalEntryErrors</c>,
/// <c>FinalizeFiscalYearHandler</c>, <c>FiscalYear.AllocateJournalEntryNumbers</c>/
/// <c>FinalizeNextDay</c>/<c>FinalizeThrough</c>, <c>JournalEntryFinalizationRepository</c> (the
/// exact renumbering SQL), and <c>CreateDraftJournalEntryHandler</c>/<c>UpdateDraftJournalEntryHandler</c>/
/// <c>AppendDraftLinesHandler</c>/<c>ReplaceDraftLinesHandler</c>/<c>PostJournalEntryHandler</c>/
/// <c>DeleteDraftJournalEntryHandler</c>/<c>ReverseJournalEntryHandler</c> in full. Key confirmed
/// facts (see also <c>ReversalScenarios.cs</c> and <c>JournalEntryPostingTests.cs</c>):
///
/// <list type="bullet">
/// <item>Both Reference Number and Journal Entry Number are allocated together, at draft creation
/// time, by <c>FiscalYear.AllocateJournalEntryNumbers()</c> — <c>PostJournalEntryHandler</c> never
/// allocates anything. "Provisional" describes <c>NumberFinalized == false</c> at creation, not a
/// delayed allocation.</item>
/// <item>Daily finalization (<c>POST .../fiscal-years/{id}/finalize</c>) only ever advances the
/// boundary by exactly one calendar day per call (<c>FinalizeFiscalYearHandler</c>); requesting the
/// current boundary again is a pure idempotent no-op (early-return, no renumbering); any other date
/// (backward, or skipping ahead) is rejected 409 <c>journal_entry_invalid_finalization_date</c>.
/// </item>
/// <item>Each successful (non-idempotent) finalize call renumbers the ENTIRE unfinalized tail
/// (every entry with <c>number_finalized = 0</c>, draft or posted, any date) by
/// <c>ORDER BY accounting_date, registered_at, reference_number</c> — only entries that are
/// <c>POSTED</c> and dated on/before the new boundary become <c>number_finalized = 1</c>; the rest
/// keep <c>number_finalized = 0</c> but may still receive a new contiguous number.</item>
/// <item>A draft can never end up dated on/before <c>FinalizedThroughDate</c>: creation and header
/// update both reject an accounting date on/before the boundary, and finalizing a day is itself
/// blocked whenever a draft is dated on that exact day
/// (<c>JournalEntryFinalizationRepository.HasBlockingDraftsAsync</c>). This makes the "reject
/// append/replace/post/delete on a finalized date" checks in those four handlers unreachable
/// defense-in-depth in practice — see NUM-015.</item>
/// </list>
/// </summary>
public sealed class DailyFinalizationScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "NUM-001")]
    public async Task ReferenceNumbers_StartAtOnePerFiscalYearAndIncreaseAtCreation()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);

        var first = await scenario.CreateDraftEntryAsync(date, "Entry one", BalancedLines());
        var second = await scenario.CreateDraftEntryAsync(date, "Entry two", BalancedLines());
        var third = await scenario.CreateDraftEntryAsync(date, "Entry three", BalancedLines());

        Assert.True(first.IsSuccess, first.RawBody);
        Assert.True(second.IsSuccess, second.RawBody);
        Assert.True(third.IsSuccess, third.RawBody);
        Assert.Equal(1, first.Value!.ReferenceNumber);
        Assert.Equal(2, second.Value!.ReferenceNumber);
        Assert.Equal(3, third.Value!.ReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-002")]
    public async Task ReferenceNumbers_AreIndependentBetweenFiscalYears()
    {
        var scenarioA = await ArrangeOpenBookAsync();
        var scenarioB = await ArrangeOpenBookUsingExistingChartAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);

        var entryA = await scenarioA.CreateDraftEntryAsync(date, "Book A entry", BalancedLines());
        var entryB1 = await scenarioB.CreateDraftEntryAsync(date, "Book B entry one", BalancedLines());
        var entryB2 = await scenarioB.CreateDraftEntryAsync(date, "Book B entry two", BalancedLines());

        Assert.True(entryA.IsSuccess, entryA.RawBody);
        Assert.True(entryB1.IsSuccess, entryB1.RawBody);
        Assert.True(entryB2.IsSuccess, entryB2.RawBody);
        Assert.Equal(1, entryA.Value!.ReferenceNumber);
        Assert.Equal(1, entryB1.Value!.ReferenceNumber);
        Assert.Equal(2, entryB2.Value!.ReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-003")]
    public async Task DeletedDraftReferenceNumber_IsNeverReused()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "To be deleted", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);
        Assert.Equal(1, draft.Value!.ReferenceNumber);

        var deleted = await Api.DeleteDraftEntryAsync(scenario.Context.FiscalYearId, draft.Value.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var next = await scenario.CreateDraftEntryAsync(date, "After delete", BalancedLines());
        Assert.True(next.IsSuccess, next.RawBody);
        Assert.Equal(2, next.Value!.ReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-004")]
    public async Task CommittedReferenceNumber_IsImmutable()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var first = await PostBalancedEntryAsync(scenario, date, amount: 100m);
        Assert.Equal(1, first.ReferenceNumber);

        // Unrelated later activity (another entry created, posted, and reversed) must not perturb
        // the already-committed reference number.
        var second = await PostBalancedEntryAsync(scenario, date.AddDays(1), amount: 200m);
        var reversal = await scenario.ReverseEntryAsync(second.ReferenceNumber, date.AddDays(2), "Unrelated reversal");
        Assert.True(reversal.IsSuccess, reversal.RawBody);

        var reloadedFirst = await Api.GetEntryAsync(scenario.Context.FiscalYearId, first.Id);
        Assert.True(reloadedFirst.IsSuccess, reloadedFirst.RawBody);
        Assert.Equal(1, reloadedFirst.Value!.ReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-005")]
    public async Task JournalEntryNumber_IsAssignedProvisionallyAtCreation()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);

        var draft = await scenario.CreateDraftEntryAsync(date, "Provisional number", BalancedLines());

        Assert.True(draft.IsSuccess, draft.RawBody);
        Assert.Equal(1, draft.Value!.JournalEntryNumber);
        Assert.False(draft.Value.NumberFinalized);
        var header = await Inspector.GetHeaderByReferenceAsync(scenario.Context.FiscalYearId, draft.Value.ReferenceNumber);
        Assert.Equal(1, header!.JournalEntryNumber);
        Assert.False(header.NumberFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-006")]
    public async Task BackdatedUnfinalizedEntry_ShouldRenumberOnlyTheUnfinalizedTail()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 5);
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-BACKDATE", start, end);
        await scenario.OpenFiscalYearAsync();
        var targetDate = start;
        var laterDate = start.AddDays(1);

        // Created out of chronological order: the later-dated entry is created FIRST, so at
        // creation time it receives the lower reference/journal-entry number pair.
        var later = await PostBalancedEntryAsync(scenario, laterDate, amount: 100m);
        var target = await PostBalancedEntryAsync(scenario, targetDate, amount: 100m);
        Assert.Equal(1, later.ReferenceNumber);
        Assert.Equal(1, later.JournalEntryNumber);
        Assert.Equal(2, target.ReferenceNumber);
        Assert.Equal(2, target.JournalEntryNumber);

        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, targetDate);
        Assert.True(finalize.IsSuccess, finalize.RawBody);

        var reloadedTarget = await Api.GetEntryAsync(scenario.Context.FiscalYearId, target.Id);
        var reloadedLater = await Api.GetEntryAsync(scenario.Context.FiscalYearId, later.Id);
        Assert.Equal(1, reloadedTarget.Value!.JournalEntryNumber);
        Assert.True(reloadedTarget.Value.NumberFinalized);
        Assert.Equal(2, reloadedLater.Value!.JournalEntryNumber);
        Assert.False(reloadedLater.Value.NumberFinalized);
        // Reference numbers, unlike journal entry numbers, are never renumbered.
        Assert.Equal(1, reloadedLater.Value.ReferenceNumber);
        Assert.Equal(2, reloadedTarget.Value.ReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-007")]
    public async Task Finalization_OrdersByAccountingDateThenRegisteredAtThenReferenceNumber()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 5);
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-ORDER", start, end);
        await scenario.OpenFiscalYearAsync();
        var day1 = start;
        var day2 = start.AddDays(1);

        // Created in this order: day2 first, then two day1 entries — proving the finalize boundary
        // (day1) reorders everything primarily by accounting date, using creation order
        // (registered_at, then reference_number) to break ties within the same date.
        var onDay2 = await PostBalancedEntryAsync(scenario, day2, amount: 100m);
        var firstOnDay1 = await PostBalancedEntryAsync(scenario, day1, amount: 100m);
        var secondOnDay1 = await PostBalancedEntryAsync(scenario, day1, amount: 100m);
        Assert.Equal(1, onDay2.ReferenceNumber);
        Assert.Equal(2, firstOnDay1.ReferenceNumber);
        Assert.Equal(3, secondOnDay1.ReferenceNumber);

        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, day1);
        Assert.True(finalize.IsSuccess, finalize.RawBody);

        var reloadedFirstOnDay1 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, firstOnDay1.Id);
        var reloadedSecondOnDay1 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, secondOnDay1.Id);
        var reloadedOnDay2 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, onDay2.Id);
        // Both day1 entries (dated before day2) are finalized ahead of the day2 entry, ordered
        // between themselves by creation order (registered_at / reference_number).
        Assert.Equal(1, reloadedFirstOnDay1.Value!.JournalEntryNumber);
        Assert.Equal(2, reloadedSecondOnDay1.Value!.JournalEntryNumber);
        Assert.Equal(3, reloadedOnDay2.Value!.JournalEntryNumber);
        Assert.True(reloadedFirstOnDay1.Value.NumberFinalized);
        Assert.True(reloadedSecondOnDay1.Value.NumberFinalized);
        Assert.False(reloadedOnDay2.Value.NumberFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-008")]
    public async Task Finalization_FreezesNumbersThroughTheSelectedDate()
    {
        var (scenario, day1, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();

        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, new DateOnly(2026, 1, 1));
        Assert.True(finalize.IsSuccess, finalize.RawBody);

        var reloadedDay1 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day1.Id);
        Assert.Equal(1, reloadedDay1.Value!.JournalEntryNumber);
        Assert.True(reloadedDay1.Value.NumberFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-009")]
    public async Task FinalizedJournalEntryNumber_NeverChanges()
    {
        var (scenario, day1, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);

        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start.AddDays(1))).IsSuccess);
        var afterSecond = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day1.Id);
        Assert.Equal(1, afterSecond.Value!.JournalEntryNumber);

        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start.AddDays(2))).IsSuccess);
        var afterThird = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day1.Id);
        Assert.Equal(1, afterThird.Value!.JournalEntryNumber);
        Assert.True(afterThird.Value.NumberFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-010")]
    public async Task NumbersAfterTheFinalizedBoundary_RemainProvisional()
    {
        var (scenario, _, day2, day3) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, new DateOnly(2026, 1, 1))).IsSuccess);

        var reloadedDay2 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day2.Id);
        var reloadedDay3 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day3.Id);
        Assert.False(reloadedDay2.Value!.NumberFinalized);
        Assert.False(reloadedDay3.Value!.NumberFinalized);
        Assert.Equal(2, reloadedDay2.Value.JournalEntryNumber);
        Assert.Equal(3, reloadedDay3.Value.JournalEntryNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-011")]
    public async Task FinalizingAnEarlierDate_DoesNotFreezeOrLockLaterEntries()
    {
        var (scenario, _, _, day3) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);

        var reloadedDay3 = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day3.Id);
        Assert.False(reloadedDay3.Value!.NumberFinalized);
        // Day 3 (two days later than the finalized boundary) remains fully open for new activity.
        var newEntryOnDay3 = await scenario.CreateDraftEntryAsync(
            new DateOnly(2026, 1, 3), "Still open for business", BalancedLines());
        Assert.True(newEntryOnDay3.IsSuccess, newEntryOnDay3.RawBody);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-012")]
    public async Task FinalizedThroughDate_CannotMoveBackward()
    {
        var (scenario, _, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start.AddDays(1))).IsSuccess);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start),
            HttpStatusCode.Conflict, JournalEntryErrors.InvalidFinalizationDate);

        var fiscalYear = await Api.GetFiscalYearAsync(scenario.Context.FiscalYearId);
        Assert.Equal(start.AddDays(1), fiscalYear.Value!.FinalizedThroughDate);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-013")]
    public async Task FinalizationCannotExceedTheEffectiveFiscalYearEnd()
    {
        var (scenario, _, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start.AddDays(1))).IsSuccess);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, end)).IsSuccess);

        // Advancing exactly one more day is beyond EffectiveEndDate — rejected by
        // FiscalYear.FinalizeThrough's own invariant (FiscalYearErrors.CannotBeFinalized), reached
        // only after the handler's "advance exactly one day" pre-check passes.
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, end.AddDays(1)),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-014")]
    public async Task FinalizedDate_RejectsEntryCreation()
    {
        var (scenario, _, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(start, "Too late", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateFinalized);
    }

    /// <summary>
    /// Only two of the six named operations are reachable through the public API with an
    /// accounting date that is ALREADY finalized (confirmed by reading
    /// <c>UpdateDraftJournalEntryHandler</c>, <c>AppendDraftLinesHandler</c>,
    /// <c>ReplaceDraftLinesHandler</c>, <c>PostJournalEntryHandler</c>,
    /// <c>DeleteDraftJournalEntryHandler</c>, <c>ReverseJournalEntryHandler</c>, and
    /// <c>JournalEntryFinalizationRepository.HasBlockingDraftsAsync</c> in full):
    ///
    /// Append/Replace/Post/Delete all validate the TARGET entry's OWN (already-persisted)
    /// <c>AccountingDate</c> against <c>FinalizedThroughDate</c>. Because daily finalization only
    /// ever advances exactly one day per call, and that single day is blocked whenever ANY draft is
    /// dated on it, a draft can never end up dated on/before a boundary that has actually been
    /// finalized — by the time a day is successfully finalized, no draft can still be dated on or
    /// before it. So those four checks are unreachable defense-in-depth, not observable red paths
    /// (documented here and in SCENARIO_COVERAGE.md rather than silently dropped, per spec
    /// §3.3/§10).
    ///
    /// Update validates the NEW (request-supplied) <c>AccountingDate</c>, which the caller freely
    /// chooses — so moving a draft's date onto an already-finalized date IS reachable. Reversal
    /// validates the reversal's OWN (request-supplied) <c>AccountingDate</c> the same way — also
    /// reachable (and the dedicated subject of REV-011).
    /// </summary>
    [Fact]
    [Trait("ScenarioId", "NUM-015")]
    public async Task FinalizedDate_RejectsDraftUpdateOntoItAndReversalDatedOnIt()
    {
        var (scenario, day1, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);

        var laterDraft = await scenario.CreateDraftEntryAsync(start.AddDays(2), "Movable draft", BalancedLines());
        Assert.True(laterDraft.IsSuccess, laterDraft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.UpdateDraftEntryAsync(scenario.Context.FiscalYearId, laterDraft.Value!.Id,
                new UpdateDraftJournalEntryRequest
                {
                    AccountingDate = start, Description = "Attempted move onto finalized date",
                    DocumentType = ScenarioDefaults.DocumentTypeGeneral, BalanceEffect = ScenarioDefaults.BalanceEffectFinancial
                }),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateFinalized);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(day1.ReferenceNumber, start, "Reversal dated on the finalized boundary"),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-016")]
    public async Task LaterUnfinalizedDates_RemainFullyMutable()
    {
        var (scenario, _, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        var laterDate = start.AddDays(2);
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start)).IsSuccess);

        var toMutate = await scenario.CreateDraftEntryAsync(laterDate, "Mutable draft", BalancedLines());
        Assert.True(toMutate.IsSuccess, toMutate.RawBody);
        var updated = await Api.UpdateDraftEntryAsync(scenario.Context.FiscalYearId, toMutate.Value!.Id,
            new UpdateDraftJournalEntryRequest
            {
                AccountingDate = laterDate, Description = "Updated while mutable",
                DocumentType = ScenarioDefaults.DocumentTypeGeneral, BalanceEffect = ScenarioDefaults.BalanceEffectFinancial
            });
        Assert.True(updated.IsSuccess, updated.RawBody);
        var appended = await Api.AppendDraftLinesAsync(scenario.Context.FiscalYearId, toMutate.Value.Id,
            [Debit("ASSET", "01", "01", 5m, "Additional debit"), Credit("EQUITY", "01", "01", 5m, "Additional credit")]);
        Assert.True(appended.IsSuccess, appended.RawBody);
        var posted = await scenario.PostEntryAsync(toMutate.Value.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        Assert.Equal("POSTED", posted.Value!.Status);

        var toDelete = await scenario.CreateDraftEntryAsync(laterDate, "Deletable draft", BalancedLines());
        Assert.True(toDelete.IsSuccess, toDelete.RawBody);
        var deleted = await Api.DeleteDraftEntryAsync(scenario.Context.FiscalYearId, toDelete.Value!.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-017")]
    public async Task FinalizationOfADateWithNoActivity_SucceedsTrivially()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 5);
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-NO-ACTIVITY", start, end);
        await scenario.OpenFiscalYearAsync();

        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start);

        Assert.True(finalize.IsSuccess, finalize.RawBody);
        Assert.Equal(start, finalize.Value!.FinalizedThroughDate);
        var counters = await Inspector.GetFiscalYearCountersAsync(scenario.Context.FiscalYearId);
        Assert.Equal(start, counters.FinalizedThroughDate);
        Assert.Equal(1, counters.NextReferenceNumber);
        Assert.Equal(1, counters.NextJournalEntryNumber);
    }

    [Fact]
    [Trait("ScenarioId", "NUM-018")]
    public async Task RepeatingFinalizationAtTheSameBoundary_IsIdempotent()
    {
        var (scenario, day1, _, _) = await ArrangeThreeDayFiscalYearWithDailyEntriesAsync();
        var start = new DateOnly(2026, 1, 1);
        var first = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start);
        Assert.True(first.IsSuccess, first.RawBody);
        var reloadedAfterFirst = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day1.Id);
        Assert.Equal(1, reloadedAfterFirst.Value!.JournalEntryNumber);

        var before = await Inspector.SnapshotAsync(scenario.Context.BookId, scenario.Context.FiscalYearId);
        var replay = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, start);
        var after = await Inspector.SnapshotAsync(scenario.Context.BookId, scenario.Context.FiscalYearId);
        var persistedAfterFirst = await Api.GetFiscalYearAsync(scenario.Context.FiscalYearId);

        Assert.True(replay.IsSuccess, replay.RawBody);
        Assert.Equal(first.Value!.FinalizedThroughDate, replay.Value!.FinalizedThroughDate);
        Assert.Equal(
            persistedAfterFirst.Value!.UpdatedAt,
            replay.Value.UpdatedAt);
        Assert.Equal(before, after);
        var reloadedAfterReplay = await Api.GetEntryAsync(scenario.Context.FiscalYearId, day1.Id);
        Assert.Equal(1, reloadedAfterReplay.Value!.JournalEntryNumber);
        Assert.True(reloadedAfterReplay.Value.NumberFinalized);
    }

    // ---------------------------------------------------------------------------------------
    // Arrangement helpers (setup only — the scenario's outcome is always asserted in the test body).
    // ---------------------------------------------------------------------------------------

    private async Task<AccountingScenario> ArrangeActiveBookWithChartAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash and Banks", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);
        return scenario;
    }

    private async Task<AccountingScenario> ArrangeOpenBookAsync()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    private async Task<AccountingScenario> ArrangeOpenBookUsingExistingChartAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    /// <summary>A 3-day fiscal year (2026-01-01..03) with one balanced financial entry posted on
    /// each day, in date order — short enough to finalize day-by-day within a single test.</summary>
    private async Task<(AccountingScenario Scenario, JournalEntryDetailResponse Day1, JournalEntryDetailResponse Day2,
        JournalEntryDetailResponse Day3)> ArrangeThreeDayFiscalYearWithDailyEntriesAsync()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 3);
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-3DAY", start, end);
        await scenario.OpenFiscalYearAsync();
        var day1 = await PostBalancedEntryAsync(scenario, start, amount: 100m);
        var day2 = await PostBalancedEntryAsync(scenario, start.AddDays(1), amount: 100m);
        var day3 = await PostBalancedEntryAsync(scenario, start.AddDays(2), amount: 100m);
        return (scenario, day1, day2, day3);
    }

    private static JournalEntryLineRequest[] BalancedLines(decimal amount = 100m) =>
        [Debit("ASSET", "01", "01", amount, "Cash in"), Credit("EQUITY", "01", "01", amount, "Capital in")];

    /// <summary>Creates and posts a two-line balanced draft entry, failing fast if either step is rejected.</summary>
    private static async Task<JournalEntryDetailResponse> PostBalancedEntryAsync(
        AccountingScenario scenario, DateOnly date, decimal amount = 100m)
    {
        var draft = await scenario.CreateDraftEntryAsync(date, "Balanced entry", BalancedLines(amount));
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
