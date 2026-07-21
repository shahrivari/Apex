using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.FiscalYears.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group F ("Fiscal Year controls", FY-001 through FY-019,
/// excluding FY-011 — spec §9.F).
///
/// FY-011 ("Closed Fiscal Year is terminal") is intentionally NOT implemented: see
/// SCENARIO_COVERAGE.md — <c>FiscalYearStatus.Closed</c> is not a reachable state through any
/// public API today. FY-012 below is the real reachable terminal-state proof (Cancelled).
///
/// Two catalogue items were adapted after reading the authoritative <c>fiscal_years.md</c> spec and
/// the current (very recently changed — see git history "enforce contiguous fiscal years") code,
/// which both require Fiscal Years in one book to form a contiguous, gap-free sequence:
/// <list type="bullet">
/// <item><b>FY-006</b> ("Gaps between Fiscal Years are allowed") contradicts invariant 5 in
/// <c>fiscal_years.md</c> ("Fiscal Years... must form a contiguous sequence without date gaps") and
/// <c>CreateFiscalYearHandler</c>, which calls <c>WouldHaveGapWithRangeAsync</c> and rejects any
/// creation that would leave a gap (409 <c>fiscal_year_dates_have_gap</c>). The capability spec is
/// authoritative over the scenario catalogue's shorthand description (spec §2), so FY-006 here
/// proves the real, current, intentional rule: creating a fiscal year with a gap is rejected.</item>
/// <item><b>FY-016</b> ("Resolution inside an allowed gap") — since gaps can never exist between
/// *created* fiscal years, the only legitimate gap the system can produce is the trailing period
/// after a Fiscal Year is cancelled before its original end date (cancellation is explicitly allowed
/// to shorten the effective end without requiring a subsequent year to fill the rest of the original
/// range). FY-016 exercises resolution for a date in that trailing gap.</item>
/// </list>
/// </summary>
public sealed class FiscalYearControlScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "FY-001")]
    public async Task DraftFiscalYear_ShouldAcceptNoAccountingActivity()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        // Intentionally not opened — remains Draft.

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(
                ScenarioDefaults.FiscalYearStart.AddDays(5), "Attempt in draft fiscal year", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, "journal_entry_fiscal_year_not_open");
    }

    [Fact]
    [Trait("ScenarioId", "FY-002")]
    public async Task OpeningFiscalYear_ShouldEnableEligibleActivity()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);

        var draft = await scenario.CreateDraftEntryAsync(date, "Activity after opening", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 100m);
    }

    [Fact]
    [Trait("ScenarioId", "FY-003")]
    public async Task AtMostOneFiscalYearMayBeOpenPerBook()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("H1 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await scenario.OpenFiscalYearAsync();
        var yearOneId = scenario.Context.FiscalYearId;

        var yearTwo = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "H2 2026", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        Assert.True(yearTwo.IsSuccess, yearTwo.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, yearTwo.Value!.Id, () => Api.OpenFiscalYearAsync(yearTwo.Value.Id),
            HttpStatusCode.Conflict, FiscalYearErrors.OpenAlreadyExists);

        var yearOneAfter = await Api.GetFiscalYearAsync(yearOneId);
        Assert.Equal("OPEN", yearOneAfter.Value!.Status);
        var yearTwoAfter = await Api.GetFiscalYearAsync(yearTwo.Value.Id);
        Assert.Equal("DRAFT", yearTwoAfter.Value!.Status);
    }

    [Fact]
    [Trait("ScenarioId", "FY-004")]
    public async Task DifferentAccountingBooks_MayEachHaveAnOpenFiscalYear()
    {
        var scenarioA = await ArrangeActiveBookWithChartAsync();
        await scenarioA.CreateFiscalYearAsync("FY-2026-A", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenarioA.OpenFiscalYearAsync();

        var bookBCode = ScenarioDefaults.UniqueCode("BOOK-B");
        var bookB = await Api.CreateBookAsync(bookBCode, bookBCode);
        Assert.True(bookB.IsSuccess, bookB.RawBody);
        Assert.True((await Api.ActivateBookAsync(bookB.Value!.Id)).IsSuccess);
        var fiscalYearB = await Api.CreateFiscalYearAsync(
            bookB.Value.Id, "FY-2026-B", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        Assert.True(fiscalYearB.IsSuccess, fiscalYearB.RawBody);
        var openB = await Api.OpenFiscalYearAsync(fiscalYearB.Value!.Id);
        Assert.True(openB.IsSuccess, openB.RawBody);

        Assert.Equal("OPEN", (await Api.GetFiscalYearAsync(scenarioA.Context.FiscalYearId)).Value!.Status);
        Assert.Equal("OPEN", (await Api.GetFiscalYearAsync(fiscalYearB.Value.Id)).Value!.Status);
    }

    [Fact]
    [Trait("ScenarioId", "FY-005")]
    public async Task OverlappingFiscalYearsInOneBook_ShouldBeRejected()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);

        var overlapping = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "Overlapping year", new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30));

        ScenarioAssertions.AssertRejected(overlapping, HttpStatusCode.Conflict, FiscalYearErrors.DatesOverlap);

        var original = await Api.GetFiscalYearAsync(scenario.Context.FiscalYearId);
        Assert.True(original.IsSuccess, original.RawBody);
        Assert.Equal("DRAFT", original.Value!.Status);
    }

    /// <summary>FY-006 adapted — see class-level doc comment: contiguity is enforced, gaps are rejected.</summary>
    [Fact]
    [Trait("ScenarioId", "FY-006")]
    public async Task CreatingFiscalYearWithGapFromExistingRange_ShouldBeRejected()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("H1 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        // Starts 2026-08-01, leaving a July gap after the first year's 2026-06-30 end.
        var withGap = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "H2 2026 with a gap", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31));

        ScenarioAssertions.AssertRejected(withGap, HttpStatusCode.Conflict, FiscalYearErrors.DatesHaveGap);

        // The contiguous continuation (no gap) is accepted, proving the rejection above was
        // specifically about the gap, not the second-year creation itself.
        var contiguous = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "H2 2026 contiguous", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        Assert.True(contiguous.IsSuccess, contiguous.RawBody);
    }

    [Fact]
    [Trait("ScenarioId", "FY-007")]
    public async Task DraftDatesAndTitle_MayBeChanged()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("Original Title", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);

        var newStart = new DateOnly(2026, 2, 1);
        var newEnd = new DateOnly(2026, 11, 30);
        var updated = await Api.UpdateFiscalYearAsync(scenario.Context.FiscalYearId, "Updated Title", newStart, newEnd);

        Assert.True(updated.IsSuccess, updated.RawBody);
        Assert.Equal("Updated Title", updated.Value!.Title);
        Assert.Equal(newStart, updated.Value.StartDate);
        Assert.Equal(newEnd, updated.Value.EndDate);
    }

    [Fact]
    [Trait("ScenarioId", "FY-008")]
    public async Task OpenFiscalYearDates_CannotBeChanged()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.UpdateFiscalYearAsync(scenario.Context.FiscalYearId, "Attempted rename",
                ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd.AddDays(-1)),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeUpdated);
    }

    [Fact]
    [Trait("ScenarioId", "FY-009")]
    public async Task EligibleUnusedDraftFiscalYear_MayBeDeleted()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("H1 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var yearTwo = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "H2 2026", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        Assert.True(yearTwo.IsSuccess, yearTwo.RawBody);

        // Deleting the trailing (edge) year cannot introduce a gap among the remaining years.
        var deleted = await Api.DeleteFiscalYearAsync(yearTwo.Value!.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await Api.GetFiscalYearAsync(yearTwo.Value.Id);
        ScenarioAssertions.AssertRejected(afterDelete, HttpStatusCode.NotFound, FiscalYearErrors.NotFound);
    }

    [Fact]
    [Trait("ScenarioId", "FY-010")]
    public async Task OpenFiscalYear_CannotBeDeleted()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.DeleteFiscalYearAsync(scenario.Context.FiscalYearId),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeDeleted);
    }

    [Fact]
    [Trait("ScenarioId", "FY-012")]
    public async Task CancelledFiscalYear_ShouldBeTerminal()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        await CancelAtStartAsync(scenario);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.OpenFiscalYearAsync(scenario.Context.FiscalYearId),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeOpened);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.UpdateFiscalYearAsync(scenario.Context.FiscalYearId, "Attempted rename",
                ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeUpdated);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.CancelFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeCancelled);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.DeleteFiscalYearAsync(scenario.Context.FiscalYearId),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeDeleted);
    }

    [Fact]
    [Trait("ScenarioId", "FY-013")]
    public async Task CancellationIsValidOnlyAtTheFinalizedBoundary()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(finalize.IsSuccess, finalize.RawBody);

        var wrongBoundary = ScenarioDefaults.FiscalYearStart.AddDays(1);
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.CancelFiscalYearAsync(scenario.Context.FiscalYearId, wrongBoundary),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.CannotBeCancelled);

        var cancel = await Api.CancelFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(cancel.IsSuccess, cancel.RawBody);
        Assert.Equal("CANCELLED", cancel.Value!.Status);
    }

    [Fact]
    [Trait("ScenarioId", "FY-014")]
    public async Task TerminalFiscalYears_ShouldRemainHistoricallyReportable()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        var date = ScenarioDefaults.FiscalYearStart;
        var draft = await scenario.CreateDraftEntryAsync(date, "Entry before cancellation", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        await CancelAtStartAsync(scenario);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 100m);
        var audit = await Api.GetJournalEntryAuditAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, posted.Value!.ReferenceNumber);
        Assert.True(audit.IsSuccess, audit.RawBody);
        Assert.Single(audit.Value!, item => item.ReferenceNumber == posted.Value.ReferenceNumber);
        Assert.Equal("CANCELLED", (await Api.GetFiscalYearAsync(scenario.Context.FiscalYearId)).Value!.Status);
    }

    [Fact]
    [Trait("ScenarioId", "FY-015")]
    public async Task FiscalYearResolution_ShouldSelectTheUniqueMatchingYear()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("H1 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var yearOneId = scenario.Context.FiscalYearId;
        var yearTwo = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "H2 2026", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        Assert.True(yearTwo.IsSuccess, yearTwo.RawBody);

        var resolvedOne = await Api.ResolveFiscalYearAsync(scenario.Context.BookId, new DateOnly(2026, 3, 15));
        Assert.True(resolvedOne.IsSuccess, resolvedOne.RawBody);
        Assert.Equal(yearOneId, resolvedOne.Value!.Id);

        var resolvedTwo = await Api.ResolveFiscalYearAsync(scenario.Context.BookId, new DateOnly(2026, 9, 15));
        Assert.True(resolvedTwo.IsSuccess, resolvedTwo.RawBody);
        Assert.Equal(yearTwo.Value!.Id, resolvedTwo.Value!.Id);
    }

    /// <summary>FY-016 adapted — see class-level doc comment: the only legitimate gap is the
    /// trailing period after a cancellation, since gaps between created years are rejected.</summary>
    [Fact]
    [Trait("ScenarioId", "FY-016")]
    public async Task ResolutionInTheGapAfterCancellation_ShouldReturnTheDocumentedNotFoundOutcome()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        await CancelAtStartAsync(scenario); // Effective end becomes FiscalYearStart; the rest of the year is now a gap.

        var result = await Api.ResolveFiscalYearAsync(scenario.Context.BookId, ScenarioDefaults.FiscalYearStart.AddDays(10));

        ScenarioAssertions.AssertRejected(result, HttpStatusCode.NotFound, FiscalYearErrors.NotFoundForDate);
    }

    [Fact]
    [Trait("ScenarioId", "FY-017")]
    public async Task OpeningOneFiscalYear_ShouldNotSilentlyCloseAnother()
    {
        var scenario = await ArrangeActiveBookWithChartAsync();
        await scenario.CreateFiscalYearAsync("H1 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await scenario.OpenFiscalYearAsync();
        var yearOneId = scenario.Context.FiscalYearId;
        var yearTwo = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "H2 2026", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        Assert.True(yearTwo.IsSuccess, yearTwo.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, yearTwo.Value!.Id, () => Api.OpenFiscalYearAsync(yearTwo.Value.Id),
            HttpStatusCode.Conflict, FiscalYearErrors.OpenAlreadyExists);

        Assert.Equal("OPEN", (await Api.GetFiscalYearAsync(yearOneId)).Value!.Status);
    }

    [Fact]
    [Trait("ScenarioId", "FY-018")]
    public async Task ArchivedBook_CannotReceiveANewFiscalYear()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        // A book must be Suspended before it can be Archived (AccountingBook.Archive rejects
        // archiving directly from Active — confirmed by reading the domain entity).
        Assert.True((await Api.SuspendBookAsync(scenario.Context.BookId)).IsSuccess);
        Assert.True((await Api.ArchiveBookAsync(scenario.Context.BookId)).IsSuccess);

        var result = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);

        ScenarioAssertions.AssertRejected(result, HttpStatusCode.UnprocessableEntity, FiscalYearErrors.AccountingBookArchived);
    }

    [Fact]
    [Trait("ScenarioId", "FY-019")]
    public async Task OnlyAnActiveBook_PermitsFiscalYearOpening()
    {
        // A Draft (never-activated) book may still receive a fiscal year, but may not open it.
        var draftBookScenario = await NewScenarioAsync();
        await draftBookScenario.CreateBookAsync();
        var draftYear = await Api.CreateFiscalYearAsync(
            draftBookScenario.Context.BookId, "FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        Assert.True(draftYear.IsSuccess, draftYear.RawBody);
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            draftBookScenario.Context.BookId, draftYear.Value!.Id, () => Api.OpenFiscalYearAsync(draftYear.Value.Id),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.AccountingBookNotActive);

        // A Suspended book (previously Active) may also not open a fiscal year.
        var suspendedBookScenario = await NewScenarioAsync();
        await suspendedBookScenario.CreateBookAsync();
        await suspendedBookScenario.ActivateBookAsync();
        Assert.True((await Api.SuspendBookAsync(suspendedBookScenario.Context.BookId)).IsSuccess);
        var suspendedYear = await Api.CreateFiscalYearAsync(suspendedBookScenario.Context.BookId, "FY-2026",
            ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        Assert.True(suspendedYear.IsSuccess, suspendedYear.RawBody);
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            suspendedBookScenario.Context.BookId, suspendedYear.Value!.Id, () => Api.OpenFiscalYearAsync(suspendedYear.Value.Id),
            HttpStatusCode.UnprocessableEntity, FiscalYearErrors.AccountingBookNotActive);
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
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.Person);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.Person);
        await scenario.SeedStandardDetailAccountAsync();
        return scenario;
    }

    private static JournalEntryLineRequest[] BalancedLines() =>
        [Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in")];

    /// <summary>Advances the fiscal year's finalized-through date to its start date, then cancels it
    /// exactly at that boundary — the only reachable path to <c>Cancelled</c>.</summary>
    private async Task CancelAtStartAsync(AccountingScenario scenario)
    {
        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(finalize.IsSuccess, finalize.RawBody);
        var cancel = await Api.CancelFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(cancel.IsSuccess, cancel.RawBody);
    }
}
