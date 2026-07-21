using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.GetTransactionReport;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group A ("Basic posting and reporting", BASIC-001
/// through BASIC-012 — spec §9.A). Each test proves one clear business outcome of the
/// Book → Fiscal Year → Chart of Accounts → draft → post → report journey through the public HTTP API.
/// </summary>
public sealed class BasicPostingScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "BASIC-001")]
    public async Task PostBalancedDraftEntry_ShouldProduceCorrectBalances()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 1);

        var draft = await scenario.CreateDraftEntryAsync(date, "Initial capital contribution",
            [Debit("ASSET", "01", "01", 1_000m, "Cash received"), Credit("EQUITY", "01", "01", 1_000m, "Owner capital")]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        Assert.Equal("POSTED", posted.Value!.Status);

        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted.Value!);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 1_000m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EQUITY", "01", "01", null, date, -1_000m);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-002")]
    [Trait("ScenarioId", "PROJ-003")]
    public async Task DraftEntry_ShouldNotAffectFinancialBalanceOrProjections()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 1);

        var draft = await scenario.CreateDraftEntryAsync(date, "Unposted capital contribution",
            [Debit("ASSET", "01", "01", 500m, "Cash pending"), Credit("EQUITY", "01", "01", 500m, "Capital pending")]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        Assert.Equal("DRAFT", draft.Value!.Status);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 0m);
        var turnoverRows = await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, "ASSET", "01", "01");
        Assert.Empty(turnoverRows);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-003")]
    public async Task MultipleEntriesOnSameDay_ShouldAggregateCorrectly()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 5);

        await PostAsync(scenario, date, "Capital contribution 1",
            Debit("ASSET", "01", "01", 300m, "Cash in"), Credit("EQUITY", "01", "01", 300m, "Capital in"));
        await PostAsync(scenario, date, "Capital contribution 2",
            Debit("ASSET", "01", "01", 700m, "Cash in"), Credit("EQUITY", "01", "01", 700m, "Capital in"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 1_000m);
        var turnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "01", "01");
        Assert.Equal(1_000m, turnover.Debit);
        Assert.Equal(0m, turnover.Credit);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-004")]
    public async Task PostEntriesAcrossSeveralDays_ShouldReturnCorrectClosingBalances()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var first = new DateOnly(2026, 3, 1);
        var second = new DateOnly(2026, 3, 10);

        await PostAsync(scenario, first, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));
        await PostAsync(scenario, second, "Cash withdrawal",
            Debit("EQUITY", "01", "01", 300m, "Capital out"), Credit("ASSET", "01", "01", 300m, "Cash out"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, first, 1_000m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, second, 700m);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-005")]
    public async Task EntryWithMoreThanTwoLines_ShouldPostWhenBalanced()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 12);

        var draft = await scenario.CreateDraftEntryAsync(date, "Mixed capital and expense funding",
            [
                Debit("ASSET", "01", "01", 300m, "Cash in"),
                Debit("EXPENSE", "01", "01", 200m, "Expense recorded"),
                Credit("EQUITY", "01", "01", 500m, "Capital in")
            ]);
        Assert.True(draft.IsSuccess, draft.RawBody);

        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        Assert.Equal(3, posted.Value!.Lines.Count);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted.Value!);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-006")]
    public async Task MultipleLinesToSameAccount_ShouldAggregateWithinOneEntry()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 15);

        var draft = await scenario.CreateDraftEntryAsync(date, "Split cash receipt",
            [
                Debit("ASSET", "01", "01", 400m, "Cash in, tranche 1"),
                Debit("ASSET", "01", "01", 600m, "Cash in, tranche 2"),
                Credit("EQUITY", "01", "01", 1_000m, "Capital in")
            ]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);

        var turnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "01", "01");
        Assert.Equal(1_000m, turnover.Debit);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-007")]
    public async Task DifferentAccounts_ShouldRemainIndependentlyReportable()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 20);

        await PostAsync(scenario, date, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));
        await PostAsync(scenario, date, "Fee revenue",
            Debit("ASSET", "01", "01", 250m, "Cash from fee"), Credit("REVENUE", "01", "01", 250m, "Fee revenue"));

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "EQUITY", "01", "01", null, date, -1_000m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "REVENUE", "01", "01", null, date, -250m);
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 1_250m);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-008")]
    public async Task TrialBalance_TotalDebitShouldEqualTotalCredit()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 22);

        await PostAsync(scenario, date, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));
        await PostAsync(scenario, date, "Expense paid from cash",
            Debit("EXPENSE", "01", "01", 150m, "Expense paid"), Credit("ASSET", "01", "01", 150m, "Cash out"));

        var report = await Api.GetTrialBalanceAsync(scenario.Context.BookId, scenario.Context.FiscalYearId, date, date);
        Assert.True(report.IsSuccess, report.RawBody);
        Assert.Equal(report.Value!.Sum(item => item.CreditTurnover), report.Value!.Sum(item => item.DebitTurnover));
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-009")]
    public async Task TransactionReport_ShouldReturnEntriesInDefinedAccountingOrder()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var later = new DateOnly(2026, 3, 25);
        var earlier = new DateOnly(2026, 3, 18);

        // Created out of chronological order — the report must still order by accounting date.
        var laterEntry = await PostAsync(scenario, later, "Later dated entry",
            Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 100m, "Capital in"));
        var earlierEntry = await PostAsync(scenario, earlier, "Earlier dated entry",
            Debit("ASSET", "01", "01", 50m, "Cash in"), Credit("EQUITY", "01", "01", 50m, "Capital in"));

        var report = await Api.GetJournalReportAsync(new GetTransactionReportRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountClassCode = "ASSET",
            GeneralAccountCode = "01",
            SubsidiaryAccountCode = "01"
        });
        Assert.True(report.IsSuccess, report.RawBody);
        var referenceOrder = report.Value!.Select(item => item.ReferenceNumber).ToList();
        var expectedOrder = new List<long> { earlierEntry.ReferenceNumber, laterEntry.ReferenceNumber };
        Assert.Equal(expectedOrder, referenceOrder);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-010")]
    public async Task JournalEntryAudit_ShouldReturnExpectedHeaderLinesStatusNumbersAndTimestamps()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 28);
        var entry = await PostAsync(scenario, date, "Audited capital contribution",
            Debit("ASSET", "01", "01", 900m, "Cash in"), Credit("EQUITY", "01", "01", 900m, "Capital in"));

        var audit = await Api.GetJournalEntryAuditAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, entry.ReferenceNumber);
        Assert.True(audit.IsSuccess, audit.RawBody);
        var record = Assert.Single(audit.Value!, item => item.ReferenceNumber == entry.ReferenceNumber);
        Assert.Equal(entry.Id, record.Id);
        Assert.Equal(entry.JournalEntryNumber, record.JournalEntryNumber);
        Assert.Equal(entry.AccountingDate, record.AccountingDate);
        Assert.Equal("POSTED", record.Status);
        Assert.Null(record.ReversalOfReferenceNumber);
        Assert.Null(record.ReversedByReferenceNumber);
        Assert.NotNull(record.PostedAt);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-011")]
    public async Task PostingEntry_ShouldAffectAuthoritativeDataAndProjectionsExactlyOnce()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var date = new DateOnly(2026, 3, 30);

        await PostAsync(scenario, date, "Single capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));

        Assert.Equal(1, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
        Assert.Equal(2, await Inspector.CountLinesAsync(scenario.Context.FiscalYearId));
        var turnover = await Inspector.GetAggregateTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, date, "ASSET", "01", "01");
        Assert.Equal(1_000m, turnover.Debit);
        Assert.Equal(0m, turnover.Credit);
    }

    [Fact]
    [Trait("ScenarioId", "BASIC-012")]
    public async Task EmptyDateRangeFilter_ShouldReturnEmptyValidResultRatherThanFabricatedZeroRows()
    {
        var scenario = await ArrangeFourAccountBookAsync();
        var activityDate = new DateOnly(2026, 4, 1);
        await PostAsync(scenario, activityDate, "Capital contribution",
            Debit("ASSET", "01", "01", 1_000m, "Cash in"), Credit("EQUITY", "01", "01", 1_000m, "Capital in"));

        var beforeAnyActivity = activityDate.AddDays(-10);
        var report = await Api.GetTrialBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, beforeAnyActivity, beforeAnyActivity);

        Assert.True(report.IsSuccess, report.RawBody);
        Assert.Empty(report.Value!);
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

    /// <summary>Creates and posts a two-line balanced draft entry, failing fast if either step is rejected.</summary>
    private static async Task<JournalEntryDetailResponse> PostAsync(
        AccountingScenario scenario,
        DateOnly date,
        string description,
        JournalEntryLineRequest debitLine,
        JournalEntryLineRequest creditLine)
    {
        var draft = await scenario.CreateDraftEntryAsync(date, description, [debitLine, creditLine]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
