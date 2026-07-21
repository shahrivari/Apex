using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group G ("Reversals", REV-001 through REV-015 —
/// spec §9.G). Reversal business rules were confirmed by reading
/// <c>ReverseJournalEntryHandler</c> and <c>JournalEntry.CreatePostedReversal</c> in full:
/// reversal swaps every line's side while preserving row number/amount/account path, links both
/// directions (<c>ReversalOfReferenceNumber</c> on the reversal, <c>ReversedByReferenceNumber</c>
/// on the original), requires the reversal accounting date to be on/after the original's date and
/// not finalized, and allocates a completely fresh Reference Number/Journal Entry Number pair
/// exactly like any new entry (<c>FiscalYear.AllocateJournalEntryNumbers</c>).
/// </summary>
public sealed class ReversalScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "REV-001")]
    public async Task PostedFinancialEntry_MayBeReversed()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 300m);

        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "Correcting entry");

        Assert.True(reversal.IsSuccess, reversal.RawBody);
        Assert.Equal("POSTED", reversal.Value!.Status);
        Assert.Equal("SYSTEM", reversal.Value.InsertionType);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(reversal.Value);
    }

    [Fact]
    [Trait("ScenarioId", "REV-002")]
    public async Task Reversal_ShouldSwapEveryLineSide_PreservingAmountsAndAccountPaths()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 450m);

        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "Swap check");
        Assert.True(reversal.IsSuccess, reversal.RawBody);

        var originalLines = original.Lines.OrderBy(line => line.RowNumber).ToList();
        var reversalLines = reversal.Value!.Lines.OrderBy(line => line.RowNumber).ToList();
        Assert.Equal(originalLines.Count, reversalLines.Count);
        for (var index = 0; index < originalLines.Count; index++)
        {
            var originalLine = originalLines[index];
            var reversalLine = reversalLines[index];
            Assert.Equal(originalLine.RowNumber, reversalLine.RowNumber);
            Assert.Equal(originalLine.Amount, reversalLine.Amount);
            Assert.Equal(originalLine.AccountClassCode, reversalLine.AccountClassCode);
            Assert.Equal(originalLine.GeneralAccountCode, reversalLine.GeneralAccountCode);
            Assert.Equal(originalLine.SubsidiaryAccountCode, reversalLine.SubsidiaryAccountCode);
            var expectedSide = originalLine.Side == ScenarioDefaults.SideDebit
                ? ScenarioDefaults.SideCredit
                : ScenarioDefaults.SideDebit;
            Assert.Equal(expectedSide, reversalLine.Side);
        }
    }

    [Fact]
    [Trait("ScenarioId", "REV-003")]
    [Trait("ScenarioId", "PROJ-002")]
    public async Task OriginalPlusReversal_ShouldHaveZeroCombinedNetEffect()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var reversalDate = date.AddDays(1);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 600m);

        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, reversalDate, "Neutralize");
        Assert.True(reversal.IsSuccess, reversal.RawBody);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, reversalDate, 0m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EQUITY", "01", "01", null, reversalDate, 0m);
    }

    [Fact]
    [Trait("ScenarioId", "REV-004")]
    public async Task OriginalAndReversal_ShouldBothRemainVisibleInAuditAndTransactionReports()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var reversalDate = date.AddDays(1);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 200m);
        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, reversalDate, "Visible in reports");
        Assert.True(reversal.IsSuccess, reversal.RawBody);

        var audit = await Api.GetJournalEntryAuditAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, original.ReferenceNumber);
        Assert.True(audit.IsSuccess, audit.RawBody);
        Assert.Equal(2, audit.Value!.Count);
        Assert.Contains(audit.Value, item => item.Id == original.Id);
        Assert.Contains(audit.Value, item => item.Id == reversal.Value!.Id);

        var transactions = await Api.GetJournalReportAsync(new GetTransactionReportRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountClassCode = "ASSET",
            GeneralAccountCode = "01",
            SubsidiaryAccountCode = "01",
            FromDate = date,
            ToDate = reversalDate
        });
        Assert.True(transactions.IsSuccess, transactions.RawBody);
        Assert.Contains(transactions.Value!, item => item.EntryId == original.Id);
        Assert.Contains(transactions.Value!, item => item.EntryId == reversal.Value!.Id);
    }

    [Fact]
    [Trait("ScenarioId", "REV-005")]
    public async Task Reversal_ShouldRecordTheOriginalReferenceNumber()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);

        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "Link check");

        Assert.True(reversal.IsSuccess, reversal.RawBody);
        Assert.Equal(original.ReferenceNumber, reversal.Value!.ReversalOfReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "REV-006")]
    public async Task Original_ShouldRecordTheReversingReferenceNumber()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);

        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "Link check");
        Assert.True(reversal.IsSuccess, reversal.RawBody);

        var reloadedOriginal = await Api.GetEntryAsync(scenario.Context.FiscalYearId, original.Id);
        Assert.True(reloadedOriginal.IsSuccess, reloadedOriginal.RawBody);
        Assert.Equal(reversal.Value!.ReferenceNumber, reloadedOriginal.Value!.ReversedByReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "REV-007")]
    public async Task ReversalReason_IsRequiredAndStored()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);

        // Required: an empty reason fails FluentValidation's NotEmpty rule before any domain check
        // runs (the same 400 pattern as JE-014's missing description — confirmed by reading
        // ReverseJournalEntryValidator).
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), ""),
            HttpStatusCode.BadRequest, "validation_failed");

        // Stored: a real reason round-trips through the response and the authoritative row.
        const string reason = "Duplicate entry corrected";
        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), reason);
        Assert.True(reversal.IsSuccess, reversal.RawBody);
        Assert.Equal(reason, reversal.Value!.ReversalReason);
        var header = await Inspector.GetHeaderByReferenceAsync(scenario.Context.FiscalYearId, reversal.Value.ReferenceNumber);
        Assert.Equal(reason, header!.ReversalReason);
    }

    [Fact]
    [Trait("ScenarioId", "REV-008")]
    public async Task DraftEntry_CannotBeReversed()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(date, "Still a draft", BalancedLines());
        Assert.True(draft.IsSuccess, draft.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(draft.Value!.ReferenceNumber, date.AddDays(1), "Cannot reverse a draft"),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.PostedImmutable);
    }

    [Fact]
    [Trait("ScenarioId", "REV-009")]
    public async Task EntryCannotBeReversedTwice()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);
        var firstReversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "First reversal");
        Assert.True(firstReversal.IsSuccess, firstReversal.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(2), "Second reversal attempt"),
            HttpStatusCode.Conflict, JournalEntryErrors.AlreadyReversed);
    }

    [Fact]
    [Trait("ScenarioId", "REV-010")]
    public async Task ReversalDate_CannotPrecedeOriginalAccountingDate()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(10);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(-1), "Cannot precede original"),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidReversalDate);
    }

    [Fact]
    [Trait("ScenarioId", "REV-011")]
    public async Task ReversalCannotOccurOnAFinalizedDate()
    {
        // Fiscal year starts exactly on the entry's date so a single finalize call reaches the
        // boundary immediately (mirrors JE-008's technique).
        var scenario = await ArrangeActiveBookWithChartAsync();
        var date = ScenarioDefaults.FiscalYearStart;
        await scenario.CreateFiscalYearAsync("FY-2026", date, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);
        var finalize = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, date);
        Assert.True(finalize.IsSuccess, finalize.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(original.ReferenceNumber, date, "Too late, date is finalized"),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.AccountingDateFinalized);
    }

    [Fact]
    [Trait("ScenarioId", "REV-012")]
    public async Task Reversal_MustRemainInTheOriginalFiscalYear()
    {
        var scenarioA = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenarioA, date, amount: 150m);

        var scenarioB = await ArrangeOpenBookUsingExistingChartAsync();

        // Attempting to reverse book A's reference number through book B's fiscal year route
        // cannot reach the original entry — it lives on a different logical partition.
        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenarioB.Context.BookId, scenarioB.Context.FiscalYearId,
            () => Api.ReverseEntryAsync(
                scenarioB.Context.FiscalYearId, original.ReferenceNumber, date.AddDays(1), "Wrong fiscal year"),
            HttpStatusCode.NotFound, JournalEntryErrors.NotFound);

        // The original entry in its real fiscal year remains untouched and reversible normally.
        var reloadedOriginal = await Api.GetEntryAsync(scenarioA.Context.FiscalYearId, original.Id);
        Assert.True(reloadedOriginal.IsSuccess, reloadedOriginal.RawBody);
        Assert.Null(reloadedOriginal.Value!.ReversedByReferenceNumber);
        var correctReversal = await scenarioA.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "Correct fiscal year");
        Assert.True(correctReversal.IsSuccess, correctReversal.RawBody);
    }

    [Fact]
    [Trait("ScenarioId", "REV-013")]
    [Trait("ScenarioId", "PROJ-004")]
    public async Task FailedReversal_ShouldLeaveOriginalLinksAndProjectionsUnchanged()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(10);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 400m);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(-1), "Invalid earlier date"),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.InvalidReversalDate);

        var reloadedOriginal = await Api.GetEntryAsync(scenario.Context.FiscalYearId, original.Id);
        Assert.True(reloadedOriginal.IsSuccess, reloadedOriginal.RawBody);
        Assert.Null(reloadedOriginal.Value!.ReversedByReferenceNumber);
        Assert.Equal("POSTED", reloadedOriginal.Value.Status);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 400m);
    }

    [Fact]
    [Trait("ScenarioId", "REV-014")]
    public async Task ReversingOneEntry_ShouldNotAffectUnrelatedAccountBalances()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var reversalDate = date.AddDays(1);
        var capitalEntry = await PostAsync(scenario, date, "Capital contribution",
            Debit("ASSET", "01", "01", 500m, "Cash in"), Credit("EQUITY", "01", "01", 500m, "Capital in"));
        await PostAsync(scenario, date, "Expense recorded",
            Debit("EXPENSE", "01", "01", 300m, "Expense in"), Credit("REVENUE", "01", "01", 300m, "Revenue in"));

        var reversal = await scenario.ReverseEntryAsync(
            capitalEntry.ReferenceNumber, reversalDate, "Only reverse the capital entry");
        Assert.True(reversal.IsSuccess, reversal.RawBody);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, reversalDate, 0m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EQUITY", "01", "01", null, reversalDate, 0m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EXPENSE", "01", "01", null, reversalDate, 300m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "REVENUE", "01", "01", null, reversalDate, -300m);
    }

    [Fact]
    [Trait("ScenarioId", "REV-015")]
    public async Task Reversal_ReceivesItsOwnFreshReferenceNumberAndProvisionalJournalEntryNumber()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var original = await PostBalancedEntryAsync(scenario, date, amount: 150m);
        Assert.Equal(1, original.ReferenceNumber);
        Assert.Equal(1, original.JournalEntryNumber);

        var reversal = await scenario.ReverseEntryAsync(original.ReferenceNumber, date.AddDays(1), "Fresh numbers");

        Assert.True(reversal.IsSuccess, reversal.RawBody);
        Assert.Equal(2, reversal.Value!.ReferenceNumber);
        Assert.Equal(2, reversal.Value.JournalEntryNumber);
        Assert.False(reversal.Value.NumberFinalized);

        // The reversal's own reference number never changes on re-fetch (immutable once assigned).
        var reloaded = await Api.GetEntryAsync(scenario.Context.FiscalYearId, reversal.Value.Id);
        Assert.Equal(2, reloaded.Value!.ReferenceNumber);
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

    private async Task<AccountingScenario> ArrangeFourAccountBookAsync()
    {
        var scenario = await ArrangeOpenBookAsync();
        await scenario.CreateAccountClassAsync("EXPENSE", "Expenses");
        await scenario.CreateGeneralAccountAsync("EXPENSE", "01", "Operating Expenses", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("EXPENSE", "01", "01", "Office Expense", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateAccountClassAsync("REVENUE", "Revenues");
        await scenario.CreateGeneralAccountAsync("REVENUE", "01", "Operating Revenue", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("REVENUE", "01", "01", "Fee Revenue", AccountNature.Creditor, DetailAccountType.None);
        return scenario;
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

    /// <summary>Creates and posts a two-line balanced draft entry between two arbitrary accounts.</summary>
    private static async Task<JournalEntryDetailResponse> PostAsync(
        AccountingScenario scenario, DateOnly date, string description,
        JournalEntryLineRequest debit, JournalEntryLineRequest credit)
    {
        var draft = await scenario.CreateDraftEntryAsync(date, description, [debit, credit]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
