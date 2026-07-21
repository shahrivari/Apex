using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using Apex.Modules.Accounting.JournalEntries.UseCases.UpdateDraftJournalEntry;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group C ("Journal Entry lifecycle and validation",
/// JE-001 through JE-030 — spec §9.C). Every red path proves the complete rejected-write contract
/// (status, problem+json, error code, trace id, zero side effects) via
/// <see cref="ScenarioAssertions.AssertRejectedWithoutSideEffectsAsync{T}"/>.
///
/// JE-004 ("Entry creation in a Closed Fiscal Year is rejected") is intentionally NOT implemented:
/// <c>FiscalYearStatus.Closed</c> is not a reachable state through any public API today (see
/// SCENARIO_COVERAGE.md / the FY-011 decision) — only Draft, Open, and Cancelled are reachable, and
/// JE-003/JE-005 already prove entry creation is rejected for Draft and Cancelled fiscal years.
/// </summary>
public sealed class JournalEntryLifecycleScenarios(ApexWebApplicationFactory factory)
    : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "JE-001")]
    public async Task CreatingEntryWithUnknownAccountingBook_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5), accountingBookId: 999_999_999L);

        // The book lookup inside JournalEntryActivityValidator always uses the fiscal year's own
        // (real) owning book, never the client-supplied AccountingBookId directly — so an unknown
        // book id surfaces through the same "fiscal year belongs to another book" cross-check as
        // JE-002, not a distinct "book not found" outcome. Confirmed by reading
        // JournalEntryActivityValidator.ValidateAsync (4-arg overload used by Create).
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateOutsideFiscalYear);
    }

    [Fact]
    [Trait("ScenarioId", "JE-002")]
    public async Task CreatingEntryWithFiscalYearFromAnotherBook_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();

        var bookBCode = ScenarioDefaults.UniqueCode("BOOK-B");
        var bookB = await Api.CreateBookAsync(bookBCode, bookBCode);
        Assert.True(bookB.IsSuccess, bookB.RawBody);
        var activateB = await Api.ActivateBookAsync(bookB.Value!.Id);
        Assert.True(activateB.IsSuccess, activateB.RawBody);
        var fiscalYearB = await Api.CreateFiscalYearAsync(
            bookB.Value.Id, "FY-2026-B", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        Assert.True(fiscalYearB.IsSuccess, fiscalYearB.RawBody);

        // Chart of Accounts is a single global hierarchy (CreateAccountClassRequest has no book id),
        // so the ASSET/EQUITY accounts created for book A already resolve for this request.
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5),
            accountingBookId: scenario.Context.BookId, fiscalYearId: fiscalYearB.Value!.Id);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            bookB.Value.Id, fiscalYearB.Value.Id, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateOutsideFiscalYear);
    }

    [Fact]
    [Trait("ScenarioId", "JE-003")]
    public async Task CreatingEntryInDraftFiscalYear_ShouldBeRejected()
    {
        var scenario = await ArrangeDraftFiscalYearBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Attempt in draft fiscal year", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.FiscalYearNotOpen);
    }

    [Fact]
    [Trait("ScenarioId", "JE-005")]
    public async Task CreatingEntryInCancelledFiscalYear_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        await CancelAtStartAsync(scenario);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(
                ScenarioDefaults.FiscalYearStart, "Attempt in cancelled fiscal year", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.FiscalYearNotOpen);
    }

    [Fact]
    [Trait("ScenarioId", "JE-006")]
    public async Task AccountingDateBeforeFiscalYearStart_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(-1);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "Before fiscal year start", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateOutsideFiscalYear);
    }

    [Fact]
    [Trait("ScenarioId", "JE-007")]
    public async Task AccountingDateAfterEffectiveFiscalYearEnd_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearEnd.AddDays(1);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(date, "After fiscal year end", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateOutsideFiscalYear);
    }

    [Fact]
    [Trait("ScenarioId", "JE-008")]
    public async Task AccountingDateOnFinalizedThroughDate_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(finalize.IsSuccess, finalize.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.CreateDraftEntryAsync(
                ScenarioDefaults.FiscalYearStart, "On the finalized boundary", BalancedLines()),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "JE-009")]
    public async Task DraftCreationWithoutLines_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5), lines: []);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("ScenarioId", "JE-010")]
    public async Task PostingWithFewerThanTwoLines_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Single line draft", [Debit("ASSET", "01", "01", 100m, "Cash in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InsufficientLines);
    }

    [Fact]
    [Trait("ScenarioId", "JE-011")]
    public async Task PostingUnbalancedEntry_ShouldBeRejectedAtomically()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Unbalanced draft",
            [Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 60m, "Capital in")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(draft.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.Unbalanced);

        Assert.Equal("DRAFT", (await Api.GetEntryAsync(scenario.Context.FiscalYearId, draft.Value!.Id)).Value!.Status);
    }

    [Fact]
    [Trait("ScenarioId", "JE-012")]
    public async Task ZeroAmountLine_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5),
            lines: [Debit("ASSET", "01", "01", 0m, "Cash in"), Credit("EQUITY", "01", "01", 0m, "Capital in")]);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("ScenarioId", "JE-013")]
    public async Task NegativeAmountLine_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5),
            lines: [Debit("ASSET", "01", "01", -50m, "Cash in"), Credit("EQUITY", "01", "01", -50m, "Capital in")]);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("ScenarioId", "JE-014")]
    public async Task MissingEntryDescription_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5), description: "");

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("ScenarioId", "JE-015")]
    public async Task MissingLineDescription_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5),
            lines: [Debit("ASSET", "01", "01", 100m, ""), Credit("EQUITY", "01", "01", 100m, "Capital in")]);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("ScenarioId", "JE-016")]
    public async Task InvalidSide_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5),
            lines:
            [
                new JournalEntryLineRequest
                {
                    Side = "SIDEWAYS", Amount = 100m, AccountClassCode = "ASSET", GeneralAccountCode = "01",
                    SubsidiaryAccountCode = "01", Description = "Cash in"
                },
                Credit("EQUITY", "01", "01", 100m, "Capital in")
            ]);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.UnsupportedSide);
    }

    [Fact]
    [Trait("ScenarioId", "JE-017")]
    public async Task UnsupportedDocumentType_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5), documentType: "BOGUS");

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.UnsupportedDocumentType);
    }

    [Fact]
    [Trait("ScenarioId", "JE-018")]
    public async Task UnsupportedInsertionType_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5), insertionType: "BOGUS");

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.UnsupportedInsertionType);
    }

    [Fact]
    [Trait("ScenarioId", "JE-019")]
    public async Task UnsupportedBalanceEffect_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = ValidRequest(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5), balanceEffect: "BOGUS");

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, () => Api.CreateDraftEntryAsync(request),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.UnsupportedBalanceEffect);
    }

    [Fact]
    [Trait("ScenarioId", "JE-020")]
    public async Task AppendingLineWithDuplicateRowNumber_ShouldBeRejected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Two line draft", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.AppendDraftLinesAsync(scenario.Context.FiscalYearId, draft.Value!.Id,
                [Debit("ASSET", "01", "01", 10m, "Duplicate row", rowNumber: 1)]),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.DuplicateRowNumber);
    }

    [Fact]
    [Trait("ScenarioId", "JE-021")]
    public async Task DraftHeader_MayBeUpdatedOnAnUnfinalizedDate()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Original description", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);

        var newDate = date.AddDays(1);
        var updated = await Api.UpdateDraftEntryAsync(scenario.Context.FiscalYearId, draft.Value!.Id,
            new UpdateDraftJournalEntryRequest
            {
                AccountingDate = newDate, Description = "Updated description",
                DocumentType = ScenarioDefaults.DocumentTypeOpening, BalanceEffect = ScenarioDefaults.BalanceEffectFinancial
            });

        Assert.True(updated.IsSuccess, updated.RawBody);
        Assert.Equal(newDate, updated.Value!.AccountingDate);
        Assert.Equal("Updated description", updated.Value.Description);
        Assert.Equal(ScenarioDefaults.DocumentTypeOpening, updated.Value.DocumentType);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(updated.Value);
    }

    [Fact]
    [Trait("ScenarioId", "JE-022")]
    public async Task DraftLines_MayBeAppended()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Draft to append to", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);

        var appended = await Api.AppendDraftLinesAsync(scenario.Context.FiscalYearId, draft.Value!.Id,
            [Debit("ASSET", "01", "01", 25m, "Additional cash in")]);

        Assert.True(appended.IsSuccess, appended.RawBody);
        Assert.Equal(3, appended.Value!.Lines.Count);
        Assert.Equal(3, appended.Value.Lines.Max(line => line.RowNumber));
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(appended.Value);
    }

    [Fact]
    [Trait("ScenarioId", "JE-023")]
    public async Task DraftLines_MayBeReplacedWithContiguousRowNumbering()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Draft to replace lines on",
            [
                Debit("ASSET", "01", "01", 100m, "Cash in"),
                Debit("ASSET", "01", "01", 50m, "More cash in"),
                Credit("EQUITY", "01", "01", 150m, "Capital in")
            ]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        var replaced = await Api.ReplaceDraftLinesAsync(scenario.Context.FiscalYearId, draft.Value!.Id,
            [
                Debit("ASSET", "01", "01", 200m, "Replacement cash in", rowNumber: 5),
                Credit("EQUITY", "01", "01", 200m, "Replacement capital in", rowNumber: 7)
            ]);

        Assert.True(replaced.IsSuccess, replaced.RawBody);
        Assert.Equal(2, replaced.Value!.Lines.Count);
        Assert.Equal(
            new[] { 1, 2 }, replaced.Value.Lines.OrderBy(line => line.RowNumber).Select(line => line.RowNumber));
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(replaced.Value);
    }

    [Fact]
    [Trait("ScenarioId", "JE-024")]
    public async Task EligibleDraft_MayBePhysicallyDeleted()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Draft to delete", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);

        var deleted = await Api.DeleteDraftEntryAsync(scenario.Context.FiscalYearId, draft.Value!.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await Api.GetEntryAsync(scenario.Context.FiscalYearId, draft.Value.Id);
        ScenarioAssertions.AssertRejected(afterDelete, HttpStatusCode.NotFound, JournalEntryErrors.NotFound);
        Assert.Equal(0, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
    }

    [Fact]
    [Trait("ScenarioId", "JE-025")]
    public async Task PostedHeader_CannotBeEdited()
    {
        var scenario = await ArrangeOpenBookAsync();
        var posted = await PostBalancedEntryAsync(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5));

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.UpdateDraftEntryAsync(scenario.Context.FiscalYearId, posted.Id,
                new UpdateDraftJournalEntryRequest
                {
                    AccountingDate = posted.AccountingDate, Description = "Attempted edit after posting",
                    DocumentType = posted.DocumentType, BalanceEffect = posted.BalanceEffect
                }),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.DraftRequired);
    }

    [Fact]
    [Trait("ScenarioId", "JE-026")]
    public async Task PostedLines_CannotBeAppendedOrReplaced()
    {
        var scenario = await ArrangeOpenBookAsync();
        var posted = await PostBalancedEntryAsync(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5));

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.AppendDraftLinesAsync(scenario.Context.FiscalYearId, posted.Id,
                [Debit("ASSET", "01", "01", 10m, "Attempted append after posting")]),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.DraftRequired);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.ReplaceDraftLinesAsync(scenario.Context.FiscalYearId, posted.Id,
                [Debit("ASSET", "01", "01", 10m, "Attempted replace after posting"),
                 Credit("EQUITY", "01", "01", 10m, "Attempted replace after posting")]),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.DraftRequired);
    }

    [Fact]
    [Trait("ScenarioId", "JE-027")]
    public async Task PostedEntry_CannotBeDeleted()
    {
        var scenario = await ArrangeOpenBookAsync();
        var posted = await PostBalancedEntryAsync(scenario, ScenarioDefaults.FiscalYearStart.AddDays(5));

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.DeleteDraftEntryAsync(scenario.Context.FiscalYearId, posted.Id),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.DraftRequired);
    }

    [Fact]
    [Trait("ScenarioId", "JE-028")]
    public async Task MissingEntry_ShouldReturnSpecifiedNotFoundError()
    {
        var scenario = await ArrangeOpenBookAsync();

        var result = await Api.GetEntryAsync(scenario.Context.FiscalYearId, 999_999_999L);

        ScenarioAssertions.AssertRejected(result, HttpStatusCode.NotFound, JournalEntryErrors.NotFound);
    }

    [Fact]
    [Trait("ScenarioId", "JE-029")]
    public async Task FailedMutation_ShouldPreserveThePreviouslyCommittedDraftExactly()
    {
        var scenario = await ArrangeOpenBookAsync();
        var originalDate = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(originalDate, "Original description", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);

        var outsideRangeDate = ScenarioDefaults.FiscalYearEnd.AddDays(5);
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => Api.UpdateDraftEntryAsync(scenario.Context.FiscalYearId, draft.Value!.Id,
                new UpdateDraftJournalEntryRequest
                {
                    AccountingDate = outsideRangeDate, Description = "Attempted rewrite",
                    DocumentType = ScenarioDefaults.DocumentTypeClosing, BalanceEffect = ScenarioDefaults.BalanceEffectFinancial
                }),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateOutsideFiscalYear);

        var preserved = await Api.GetEntryAsync(scenario.Context.FiscalYearId, draft.Value!.Id);
        Assert.True(preserved.IsSuccess, preserved.RawBody);
        Assert.Equal("Original description", preserved.Value!.Description);
        Assert.Equal(originalDate, preserved.Value.AccountingDate);
        Assert.Equal(ScenarioDefaults.DocumentTypeGeneral, preserved.Value.DocumentType);
        Assert.Equal("DRAFT", preserved.Value.Status);
    }

    [Fact]
    [Trait("ScenarioId", "JE-030")]
    public async Task PostingTheSameEntryTwice_ShouldFailWithoutDuplicatingProjectionMovement()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var posted = await PostBalancedEntryAsync(scenario, date, amount: 400m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 400m);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(posted.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.DraftRequired);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 400m);
    }

    // ---------------------------------------------------------------------------------------
    // Arrangement helpers (setup only — the scenario's outcome is always asserted in the test body).
    // ---------------------------------------------------------------------------------------

    private async Task<AccountingScenario> ArrangeOpenBookAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        await AddSimpleChartAsync(scenario);
        return scenario;
    }

    private async Task<AccountingScenario> ArrangeDraftFiscalYearBookAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        // Intentionally not opened — the fiscal year remains Draft.
        await AddSimpleChartAsync(scenario);
        return scenario;
    }

    private static async Task AddSimpleChartAsync(AccountingScenario scenario)
    {
        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash and Banks", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.Person);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.Person);
        await scenario.SeedStandardDetailAccountAsync();
    }

    private static JournalEntryLineRequest[] BalancedLines(decimal amount = 100m) =>
        [Debit("ASSET", "01", "01", amount, "Cash in"), Credit("EQUITY", "01", "01", amount, "Capital in")];

    /// <summary>
    /// Builds an otherwise-valid create request, letting each red-path test override exactly the
    /// one field under test. <see cref="CreateDraftJournalEntryRequest"/> is a plain class (not a
    /// record), so callers cannot use <c>with</c> — every variant goes through this one factory.
    /// </summary>
    private static CreateDraftJournalEntryRequest ValidRequest(
        AccountingScenario scenario, DateOnly date, long? accountingBookId = null, long? fiscalYearId = null,
        string? description = null, string? documentType = null, string? insertionType = null,
        string? balanceEffect = null, IReadOnlyList<JournalEntryLineRequest>? lines = null) => new()
        {
            AccountingBookId = accountingBookId ?? scenario.Context.BookId,
            FiscalYearId = fiscalYearId ?? scenario.Context.FiscalYearId,
            AccountingDate = date,
            Description = description ?? "Valid baseline request",
            DocumentType = documentType ?? ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = insertionType ?? ScenarioDefaults.InsertionTypeManual,
            BalanceEffect = balanceEffect ?? ScenarioDefaults.BalanceEffectFinancial,
            Lines = lines ?? BalancedLines()
        };

    /// <summary>Advances the fiscal year's finalized-through date to its start date, then cancels it
    /// exactly at that boundary — the only reachable path to <c>Cancelled</c> (spec: cancellation
    /// date must equal the finalized-through date exactly).</summary>
    private async Task CancelAtStartAsync(AccountingScenario scenario)
    {
        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(finalize.IsSuccess, finalize.RawBody);
        var cancel = await Api.CancelFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(cancel.IsSuccess, cancel.RawBody);
    }

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
