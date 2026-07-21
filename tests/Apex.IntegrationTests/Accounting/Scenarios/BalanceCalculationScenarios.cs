using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the canonical three-entry balance scenario (spec §8) and the required scenario
/// catalogue group B ("Balance boundaries and aggregation", BAL-001 through BAL-016 — spec §9.B).
/// Every closing-balance assertion goes through <see cref="ScenarioAssertions.AssertClosingBalanceAsync"/>,
/// which independently checks the public balances report, the daily-balance projection, and a
/// movement recomputed directly from posted financial lines.
/// </summary>
public sealed class BalanceCalculationScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "CANONICAL")]
    public async Task PostThreeEntriesAcrossDates_ShouldProduceCanonicalClosingBalances()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var day2 = new DateOnly(2026, 1, 2);
        var day5 = new DateOnly(2026, 1, 5);
        var day10 = new DateOnly(2026, 1, 10);

        var first = await PostAsync(scenario, day2, "Owner invests capital",
            Debit("ASSET", "01", "01", 1_000m, "Cash received"), Credit("EQUITY", "01", "01", 1_000m, "Owner capital"));
        var second = await PostAsync(scenario, day5, "Operating expense paid in cash",
            Debit("EXPENSE", "01", "01", 200m, "Expense paid"), Credit("ASSET", "01", "01", 200m, "Cash paid"));
        var third = await PostAsync(scenario, day10, "Cash sale",
            Debit("ASSET", "01", "01", 500m, "Cash received"), Credit("REVENUE", "01", "01", 500m, "Revenue earned"));

        // Each entry and its ordered lines match the authoritative journal_entry/journal_entry_line state.
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(first);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(second);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(third);

        // Closing balances at 2026-01-10, agreeing across report, projection, and authoritative recomputation.
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, day10, 1_300m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EQUITY", "01", "01", null, day10, -1_000m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EXPENSE", "01", "01", null, day10, 200m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "REVENUE", "01", "01", null, day10, -500m);

        // Daily turnover: debit/credit independently correct per the worked example in spec §8.
        var cashTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, day2, day10, "ASSET", "01", "01");
        Assert.Equal(1_500m, cashTurnover.Debit);
        Assert.Equal(200m, cashTurnover.Credit);

        var capitalTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, day2, day10, "EQUITY", "01", "01");
        Assert.Equal(0m, capitalTurnover.Debit);
        Assert.Equal(1_000m, capitalTurnover.Credit);

        var expenseTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, day2, day10, "EXPENSE", "01", "01");
        Assert.Equal(200m, expenseTurnover.Debit);
        Assert.Equal(0m, expenseTurnover.Credit);

        var revenueTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, day2, day10, "REVENUE", "01", "01");
        Assert.Equal(0m, revenueTurnover.Debit);
        Assert.Equal(500m, revenueTurnover.Credit);

        // Total debit equals total credit across the whole book.
        var trialBalance = await Api.GetTrialBalanceAsync(scenario.Context.BookId, scenario.Context.FiscalYearId, day2, day10);
        Assert.True(trialBalance.IsSuccess, trialBalance.RawBody);
        Assert.Equal(trialBalance.Value!.Sum(item => item.CreditTurnover), trialBalance.Value!.Sum(item => item.DebitTurnover));
    }

    [Fact]
    [Trait("ScenarioId", "BAL-001")]
    public async Task BalanceBeforeFirstActivityDate_ShouldBeZero()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var activityDate = new DateOnly(2026, 2, 10);
        await PostAsync(scenario, activityDate, "Capital contribution",
            Debit("ASSET", "01", "01", 400m, "Cash in"), Credit("EQUITY", "01", "01", 400m, "Capital in"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null,
            activityDate.AddDays(-1), 0m);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-002")]
    public async Task BalanceOnFirstActivityDate_ShouldIncludeThatDate()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var activityDate = new DateOnly(2026, 2, 10);
        await PostAsync(scenario, activityDate, "Capital contribution",
            Debit("ASSET", "01", "01", 400m, "Cash in"), Credit("EQUITY", "01", "01", 400m, "Capital in"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, activityDate, 400m);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-003")]
    public async Task BalanceBetweenActivityDates_ShouldCarryPriorMovement()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var first = new DateOnly(2026, 2, 1);
        var middle = new DateOnly(2026, 2, 10);
        var last = new DateOnly(2026, 2, 20);

        await PostAsync(scenario, first, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));
        await PostAsync(scenario, last, "Cash withdrawal",
            Debit("EQUITY", "01", "01", 100m, "Capital out"), Credit("ASSET", "01", "01", 100m, "Cash out"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, middle, 1_000m);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-004")]
    public async Task BalanceOnFinalActivityDate_ShouldIncludeAllMovementsThroughThatDate()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var first = new DateOnly(2026, 2, 1);
        var last = new DateOnly(2026, 2, 20);

        await PostAsync(scenario, first, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));
        await PostAsync(scenario, last, "Cash withdrawal",
            Debit("EQUITY", "01", "01", 100m, "Capital out"), Credit("ASSET", "01", "01", 100m, "Cash out"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, last, 900m);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-005")]
    public async Task BalanceAfterFinalActivityDate_ShouldRemainUnchanged()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var last = new DateOnly(2026, 2, 20);
        await PostAsync(scenario, last, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null,
            last.AddDays(30), 1_000m);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-006")]
    public async Task PeriodReport_ShouldSeparateOpeningBalanceFromInPeriodTurnover()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var openingDate = new DateOnly(2026, 2, 1);
        var periodStart = new DateOnly(2026, 2, 10);
        var periodEnd = new DateOnly(2026, 2, 20);
        var inPeriodDate = new DateOnly(2026, 2, 15);

        await PostAsync(scenario, openingDate, "Prior capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));
        await PostAsync(scenario, inPeriodDate, "In-period cash sale",
            Debit("ASSET", "01", "01", 300m, "Cash in"), Credit("REVENUE", "01", "01", 300m, "Revenue earned"));

        var report = await Api.GetTrialBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, periodStart, periodEnd);
        Assert.True(report.IsSuccess, report.RawBody);
        var cashRow = Assert.Single(report.Value!, item => item.AccountClassCode == "ASSET");
        Assert.Equal(1_000m, cashRow.OpeningBalance);
        Assert.Equal(300m, cashRow.DebitTurnover);
        Assert.Equal(0m, cashRow.CreditTurnover);
        Assert.Equal(1_300m, cashRow.ClosingBalance);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-007")]
    public async Task DebitAndCreditTurnover_ShouldBeIndependentlyCorrect()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 2, 5);

        await PostAsync(scenario, date, "Capital contribution",
            Debit("ASSET", "01", "01", 800m, "Cash in"), Credit("EQUITY", "01", "01", 800m, "Capital in"));
        await PostAsync(scenario, date, "Expense paid from cash",
            Debit("EXPENSE", "01", "01", 150m, "Expense paid"), Credit("ASSET", "01", "01", 150m, "Cash out"));

        var turnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "01", "01");
        Assert.Equal(800m, turnover.Debit);
        Assert.Equal(150m, turnover.Credit);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-008")]
    public async Task NetBalance_ShouldUseDebitPositiveCreditNegativeSemantics()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 2, 6);
        await PostAsync(scenario, date, "Capital contribution",
            Debit("ASSET", "01", "01", 700m, "Cash in"), Credit("EQUITY", "01", "01", 700m, "Capital in"));

        // Cash is debit-positive; Capital (its counterparty) is credit-negative — opposite signs, same magnitude.
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 700m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EQUITY", "01", "01", null, date, -700m);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-009")]
    public async Task DocumentTypeFiltering_ShouldIncludeOnlyRequestedActivity()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 2, 7);

        await PostAsync(scenario, date, "General capital movement",
            Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in"),
            documentType: ScenarioDefaults.DocumentTypeGeneral);
        await PostAsync(scenario, date, "Opening balance movement",
            Debit("ASSET", "01", "01", 50m, "Cash in"), Credit("EQUITY", "01", "01", 50m, "Capital in"),
            documentType: ScenarioDefaults.DocumentTypeOpening);

        var combined = await Api.GetTrialBalanceAsync(scenario.Context.BookId, scenario.Context.FiscalYearId, date, date);
        Assert.True(combined.IsSuccess, combined.RawBody);
        var combinedCash = Assert.Single(combined.Value!, item => item.AccountClassCode == "ASSET");
        Assert.Equal(150m, combinedCash.DebitTurnover);

        var openingExcluded = await Api.GetTrialBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date,
            [ScenarioDefaults.DocumentTypeOpening]);
        Assert.True(openingExcluded.IsSuccess, openingExcluded.RawBody);
        var filteredCash = Assert.Single(openingExcluded.Value!, item => item.AccountClassCode == "ASSET");
        Assert.Equal(100m, filteredCash.DebitTurnover);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-010")]
    public async Task AccountClassAggregation_ShouldCombineDescendantAccounts()
    {
        var scenario = await ArrangeAggregationBookAsync();
        var date = new DateOnly(2026, 2, 8);

        await PostAsync(scenario, date, "Cash operating movement",
            Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in"));
        await PostAsync(scenario, date, "Cash reserve movement",
            Debit("ASSET", "01", "02", 200m, "Cash in"), Credit("EQUITY", "01", "01", 200m, "Capital in"));

        var classTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET");
        Assert.Equal(300m, classTurnover.Debit);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-011")]
    public async Task GeneralAccountAggregation_ShouldCombineSubsidiaryAccounts()
    {
        var scenario = await ArrangeAggregationBookAsync();
        var date = new DateOnly(2026, 2, 9);

        await PostAsync(scenario, date, "Cash operating movement",
            Debit("ASSET", "01", "01", 120m, "Cash in"), Credit("EQUITY", "01", "01", 120m, "Capital in"));
        await PostAsync(scenario, date, "Cash reserve movement",
            Debit("ASSET", "01", "02", 80m, "Cash in"), Credit("EQUITY", "01", "01", 80m, "Capital in"));

        var generalTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "01");
        Assert.Equal(200m, generalTurnover.Debit);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-012")]
    public async Task SubsidiaryAccountResult_ShouldCombineDetailAccounts()
    {
        var scenario = await ArrangeAggregationBookAsync();
        var date = new DateOnly(2026, 2, 11);

        await PostAsync(scenario, date, "Bank detail A movement",
            Debit("ASSET", "02", "01", 60m, "Cash in", "BANK-A"), Credit("EQUITY", "01", "01", 60m, "Capital in"));
        await PostAsync(scenario, date, "Bank detail B movement",
            Debit("ASSET", "02", "01", 40m, "Cash in", "BANK-B"), Credit("EQUITY", "01", "01", 40m, "Capital in"));

        var subsidiaryTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "02", "01");
        Assert.Equal(100m, subsidiaryTurnover.Debit);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-013")]
    public async Task DetailAccountFiltering_ShouldIsolateOneDetailCode()
    {
        var scenario = await ArrangeAggregationBookAsync();
        var date = new DateOnly(2026, 2, 12);

        await PostAsync(scenario, date, "Bank detail A movement",
            Debit("ASSET", "02", "01", 60m, "Cash in", "BANK-A"), Credit("EQUITY", "01", "01", 60m, "Capital in"));
        await PostAsync(scenario, date, "Bank detail B movement",
            Debit("ASSET", "02", "01", 40m, "Cash in", "BANK-B"), Credit("EQUITY", "01", "01", 40m, "Capital in"));

        var detailATurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "02", "01", "BANK-A");
        Assert.Equal(60m, detailATurnover.Debit);

        var detailBTurnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "02", "01", "BANK-B");
        Assert.Equal(40m, detailBTurnover.Debit);
    }

    [Fact]
    [Trait("ScenarioId", "BAL-014")]
    public async Task Pagination_ShouldNotChangeTotalsOrOmitDuplicateTransactions()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var baseDate = new DateOnly(2026, 2, 13);
        var referenceNumbers = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var entry = await PostAsync(scenario, baseDate.AddDays(i), $"Cash movement {i + 1}",
                Debit("ASSET", "01", "01", 10m * (i + 1), "Cash in"),
                Credit("EQUITY", "01", "01", 10m * (i + 1), "Capital in"));
            referenceNumbers.Add(entry.ReferenceNumber);
        }

        var page1 = await Api.GetGeneralLedgerReportAsync(new GetTransactionReportRequest
        {
            AccountingBookId = scenario.Context.BookId, FiscalYearId = scenario.Context.FiscalYearId,
            AccountClassCode = "ASSET", GeneralAccountCode = "01", SubsidiaryAccountCode = "01", Page = 1, PageSize = 2
        });
        var page2 = await Api.GetGeneralLedgerReportAsync(new GetTransactionReportRequest
        {
            AccountingBookId = scenario.Context.BookId, FiscalYearId = scenario.Context.FiscalYearId,
            AccountClassCode = "ASSET", GeneralAccountCode = "01", SubsidiaryAccountCode = "01", Page = 2, PageSize = 2
        });
        var page3 = await Api.GetGeneralLedgerReportAsync(new GetTransactionReportRequest
        {
            AccountingBookId = scenario.Context.BookId, FiscalYearId = scenario.Context.FiscalYearId,
            AccountClassCode = "ASSET", GeneralAccountCode = "01", SubsidiaryAccountCode = "01", Page = 3, PageSize = 2
        });
        Assert.True(page1.IsSuccess, page1.RawBody);
        Assert.True(page2.IsSuccess, page2.RawBody);
        Assert.True(page3.IsSuccess, page3.RawBody);

        var pagedReferenceNumbers = page1.Value!.Concat(page2.Value!).Concat(page3.Value!)
            .Select(item => item.ReferenceNumber).ToList();
        Assert.Equal(referenceNumbers.Count, pagedReferenceNumbers.Count);
        Assert.Equal(referenceNumbers.Count, pagedReferenceNumbers.Distinct().Count());
        Assert.Equal(referenceNumbers.OrderBy(x => x), pagedReferenceNumbers.OrderBy(x => x));
    }

    [Fact]
    [Trait("ScenarioId", "BAL-015")]
    public async Task VeryLargeValidDecimalAmount_ShouldBeHandledWithoutOverflow()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 2, 14);
        const decimal largeAmount = 999999999999999.9999m; // DECIMAL(19,4) schema bound: 15 integer digits, 4 fractional.

        var posted = await PostAsync(scenario, date, "Very large capital contribution",
            Debit("ASSET", "01", "01", largeAmount, "Cash in"), Credit("EQUITY", "01", "01", largeAmount, "Capital in"));

        Assert.Equal(largeAmount, posted.Lines.Single(line => line.Side == "DEBIT").Amount);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, largeAmount);
    }

    /// <summary>
    /// BAL-016 — investigates, rather than assumes, fractional-amount behavior: the schema
    /// (<c>journal_entry_line.amount DECIMAL(19,4)</c>) only enforces <c>amount &gt; 0</c>; no
    /// scale/precision validation exists anywhere in the stack (confirmed by reading
    /// <c>JournalEntryLineRequestValidator</c> and the shard migration).
    ///
    /// Observed behavior (see final handoff for the full write-up): posting a 5-decimal-place
    /// amount does NOT throw and does NOT get rejected by validation. SQL Server silently rounds
    /// it to the column's DECIMAL(19,4) scale on insert (round-half-away-from-zero: 100.12345 →
    /// 100.1235) with no warning surfaced to the caller. This is the authoritative, persisted, and
    /// subsequently re-readable value — asserted below against both the database row and a fresh
    /// GET. The immediate 201 Created response, however, echoes back the unrounded 100.12345 (the
    /// in-memory value built from the request, before the database round-trip), so the create
    /// response and the persisted state disagree for one response — a discovered production defect,
    /// documented in the handoff and intentionally NOT fixed here (production changes require an
    /// unambiguous specification, and none exists for money/decimal precision policy).
    /// </summary>
    [Fact]
    [Trait("ScenarioId", "BAL-016")]
    public async Task FractionalAmountBeyondSchemaScale_ShouldRoundToSchemaScaleWithoutServerError()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 2, 16);
        const decimal fiveDecimalAmount = 100.12345m;
        const decimal expectedPersistedAmount = 100.1235m;

        var draft = await scenario.CreateDraftEntryAsync(date, "Fractional amount beyond schema scale",
            [Debit("ASSET", "01", "01", fiveDecimalAmount, "Cash in"),
             Credit("EQUITY", "01", "01", fiveDecimalAmount, "Capital in")]);

        // No unhandled 500 for ordinary user input, and no validation rejection either.
        Assert.NotEqual(HttpStatusCode.InternalServerError, draft.StatusCode);
        Assert.True(draft.IsSuccess, draft.RawBody);

        var persistedLines = await Inspector.GetOrderedLinesAsync(draft.Value!.Id);
        Assert.Equal(expectedPersistedAmount, Assert.Single(persistedLines, l => l.Side == "DEBIT").Amount);

        var fetched = await Api.GetEntryAsync(scenario.Context.FiscalYearId, draft.Value.Id);
        Assert.True(fetched.IsSuccess, fetched.RawBody);
        Assert.Equal(expectedPersistedAmount, fetched.Value!.Lines.Single(l => l.Side == "DEBIT").Amount);
    }

    // ---------------------------------------------------------------------------------------
    // Arrangement helpers (setup only — the scenario's outcome is always asserted in the test body).
    // ---------------------------------------------------------------------------------------

    private async Task<AccountingScenario> ArrangeFourAccountBookAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();

        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash and Banks", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.None);

        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);

        await scenario.CreateAccountClassAsync("EXPENSE", "Expenses");
        await scenario.CreateGeneralAccountAsync("EXPENSE", "01", "Operating Expenses", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("EXPENSE", "01", "01", "Expense", AccountNature.Debtor, DetailAccountType.None);

        await scenario.CreateAccountClassAsync("REVENUE", "Revenue");
        await scenario.CreateGeneralAccountAsync("REVENUE", "01", "Operating Revenue", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("REVENUE", "01", "01", "Revenue", AccountNature.Creditor, DetailAccountType.None);

        return scenario;
    }

    /// <summary>
    /// Chart built specifically for aggregation rollups: ASSET/01 has two Subsidiary Accounts
    /// (BAL-011), ASSET has a second General Account (BAL-010), and ASSET/02/01 requires Bank
    /// Detail Accounts so two detail codes can be posted and isolated (BAL-012/BAL-013).
    /// </summary>
    private async Task<AccountingScenario> ArrangeAggregationBookAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();

        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash and Banks", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "01", "Cash Operating", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "01", "02", "Cash Reserve", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateGeneralAccountAsync("ASSET", "02", "Bank Accounts", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync("ASSET", "02", "01", "Bank Detail Holder", AccountNature.Debtor, DetailAccountType.Bank);

        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Owner Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync("EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);

        await scenario.CreateDetailAccountAsync("BANK-A", "Bank Detail A", ScenarioDefaults.DetailAccountTypeBank);
        await scenario.CreateDetailAccountAsync("BANK-B", "Bank Detail B", ScenarioDefaults.DetailAccountTypeBank);

        return scenario;
    }

    /// <summary>Creates and posts a two-line balanced draft entry, failing fast if either step is rejected.</summary>
    private static async Task<JournalEntryDetailResponse> PostAsync(
        AccountingScenario scenario,
        DateOnly date,
        string description,
        JournalEntryLineRequest debitLine,
        JournalEntryLineRequest creditLine,
        string documentType = ScenarioDefaults.DocumentTypeGeneral)
    {
        var draft = await scenario.CreateDraftEntryAsync(date, description, [debitLine, creditLine], documentType: documentType);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
