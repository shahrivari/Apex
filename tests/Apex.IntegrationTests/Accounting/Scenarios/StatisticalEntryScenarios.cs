using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

/// <summary>
/// Covers the required scenario catalogue group H ("Statistical entries", STAT-001 through
/// STAT-008 — spec §9.H). A Statistical entry is created with
/// <c>BalanceEffect = ScenarioDefaults.BalanceEffectStatistical</c> on the same create/post
/// endpoints as a Financial entry — there is no separate endpoint. Confirmed by reading
/// <c>JournalEntry.Post</c> (the balance invariant applies regardless of
/// <see cref="BalanceEffect"/>) and both <c>PostJournalEntryHandler</c> /
/// <c>ReverseJournalEntryHandler</c>, which only call
/// <c>projectionWriteRepository.ApplyPostingAsync</c> when
/// <c>entry.BalanceEffect == BalanceEffect.Financial</c> — Statistical postings and reversals
/// never touch <c>daily_account_turnover</c>/<c>daily_account_balance</c> at all.
/// </summary>
public sealed class StatisticalEntryScenarios(ApexWebApplicationFactory factory) : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "STAT-001")]
    public async Task StatisticalEntry_HasAnIndependentDraftAndPostedLifecycle()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);

        var draft = await scenario.CreateDraftEntryAsync(
            date, "Statistical memo entry", BalancedLines(), balanceEffect: ScenarioDefaults.BalanceEffectStatistical);
        Assert.True(draft.IsSuccess, draft.RawBody);
        Assert.Equal("DRAFT", draft.Value!.Status);
        Assert.Equal(ScenarioDefaults.BalanceEffectStatistical, draft.Value.BalanceEffect);

        var posted = await scenario.PostEntryAsync(draft.Value.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        Assert.Equal("POSTED", posted.Value!.Status);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(posted.Value);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-002")]
    public async Task PostedStatisticalEntry_MustSatisfyTheBalancingRule()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var unbalanced = await scenario.CreateDraftEntryAsync(date, "Unbalanced statistical draft",
            [Debit("ASSET", "01", "01", 100m, "Cash in"), Credit("EQUITY", "01", "01", 60m, "Capital in")],
            balanceEffect: ScenarioDefaults.BalanceEffectStatistical);
        Assert.True(unbalanced.IsSuccess, unbalanced.RawBody);

        await Assertions.AssertRejectedWithoutSideEffectsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            () => scenario.PostEntryAsync(unbalanced.Value!.ReferenceNumber),
            HttpStatusCode.UnprocessableEntity, JournalEntryErrors.Unbalanced);

        // A balanced statistical entry posts normally, proving the rejection above was specifically
        // about the balance, not statistical entries being unpostable.
        var balanced = await scenario.CreateDraftEntryAsync(date, "Balanced statistical draft", BalancedLines(),
            balanceEffect: ScenarioDefaults.BalanceEffectStatistical);
        Assert.True(balanced.IsSuccess, balanced.RawBody);
        var posted = await scenario.PostEntryAsync(balanced.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-003")]
    public async Task PostedStatisticalEntry_AppearsInJournalEntryHistory()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var draft = await scenario.CreateDraftEntryAsync(
            date, "Statistical memo entry", BalancedLines(), balanceEffect: ScenarioDefaults.BalanceEffectStatistical);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);

        var search = await Api.SearchJournalEntriesAsync(
            scenario.Context.FiscalYearId, balanceEffect: ScenarioDefaults.BalanceEffectStatistical);
        Assert.True(search.IsSuccess, search.RawBody);
        Assert.Contains(search.Value!.Items, item => item.ReferenceNumber == posted.Value!.ReferenceNumber);

        var audit = await Api.GetJournalEntryAuditAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, posted.Value!.ReferenceNumber);
        Assert.True(audit.IsSuccess, audit.RawBody);
        Assert.Single(audit.Value!, item => item.Id == posted.Value.Id);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-004")]
    [Trait("ScenarioId", "PROJ-006")]
    public async Task StatisticalEntry_DoesNotAffectFinancialDailyTurnover()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var posted = await PostStatisticalEntryAsync(scenario, date, amount: 900m);
        Assert.Equal("POSTED", posted.Status);

        var turnoverRows = await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, "ASSET", "01", "01");
        Assert.Empty(turnoverRows);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-005")]
    [Trait("ScenarioId", "PROJ-007")]
    public async Task StatisticalEntry_DoesNotAffectFinancialDailyBalance()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        await PostStatisticalEntryAsync(scenario, date, amount: 900m);

        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 0m);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-006")]
    public async Task StatisticalEntry_DoesNotAffectFinancialTrialBalance()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var financial = await scenario.CreateDraftEntryAsync(date, "Financial movement", BalancedLines(250m));
        Assert.True(financial.IsSuccess, financial.RawBody);
        var postedFinancial = await scenario.PostEntryAsync(financial.Value!.ReferenceNumber);
        Assert.True(postedFinancial.IsSuccess, postedFinancial.RawBody);
        await PostStatisticalEntryAsync(scenario, date, amount: 900m);

        var report = await Api.GetTrialBalanceAsync(scenario.Context.BookId, scenario.Context.FiscalYearId, date, date);
        Assert.True(report.IsSuccess, report.RawBody);
        var assetRow = Assert.Single(report.Value!, item => item.AccountClassCode == "ASSET");
        Assert.Equal(250m, assetRow.DebitTurnover);
        Assert.Equal(250m, assetRow.ClosingBalance);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-007")]
    public async Task ReversingAStatisticalEntry_RemainsExcludedFromFinancialProjections()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var reversalDate = date.AddDays(1);
        var posted = await PostStatisticalEntryAsync(scenario, date, amount: 900m);

        var reversal = await scenario.ReverseEntryAsync(posted.ReferenceNumber, reversalDate, "Reverse statistical entry");
        Assert.True(reversal.IsSuccess, reversal.RawBody);
        Assert.Equal(ScenarioDefaults.BalanceEffectStatistical, reversal.Value!.BalanceEffect);

        Assert.Empty(await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, "ASSET", "01", "01"));
        Assert.Empty(await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, reversalDate, "ASSET", "01", "01"));
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, reversalDate, 0m);
    }

    [Fact]
    [Trait("ScenarioId", "STAT-008")]
    [Trait("ScenarioId", "PROJ-014")]
    public async Task ProjectionRebuild_ContinuesToExcludeStatisticalEntries()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(5);
        var financial = await scenario.CreateDraftEntryAsync(date, "Financial movement", BalancedLines(400m));
        Assert.True(financial.IsSuccess, financial.RawBody);
        var postedFinancial = await scenario.PostEntryAsync(financial.Value!.ReferenceNumber);
        Assert.True(postedFinancial.IsSuccess, postedFinancial.RawBody);
        await PostStatisticalEntryAsync(scenario, date, amount: 900m);

        var rebuild = await Api.RebuildJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);
        Assert.True(rebuild.IsSuccess, rebuild.RawBody);

        // The financial entry's projection is intact (rebuild preserved it)...
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01", null, date, 400m);
        // ...and the statistical entry still contributed nothing to the rebuilt turnover.
        var turnoverRows = await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, "ASSET", "01", "01");
        Assert.Single(turnoverRows);
        Assert.Equal(400m, turnoverRows[0].DebitTurnover);
        Assert.Equal(ScenarioDefaults.DocumentTypeGeneral, turnoverRows[0].DocumentType);
    }

    // ---------------------------------------------------------------------------------------
    // Arrangement helpers (setup only — the scenario's outcome is always asserted in the test body).
    // ---------------------------------------------------------------------------------------

    private async Task<AccountingScenario> ArrangeOpenBookAsync()
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
        await scenario.CreateFiscalYearAsync("FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    private static JournalEntryLineRequest[] BalancedLines(decimal amount = 100m) =>
        [Debit("ASSET", "01", "01", amount, "Cash in"), Credit("EQUITY", "01", "01", amount, "Capital in")];

    private static async Task<JournalEntryDetailResponse> PostStatisticalEntryAsync(
        AccountingScenario scenario, DateOnly date, decimal amount)
    {
        var draft = await scenario.CreateDraftEntryAsync(
            date, "Statistical memo entry", BalancedLines(amount), balanceEffect: ScenarioDefaults.BalanceEffectStatistical);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
