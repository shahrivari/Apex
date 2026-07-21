using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

public sealed class ProjectionIntegrityScenarios(ApexWebApplicationFactory factory)
    : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "PROJ-001")]
    public async Task HealthyPostedEntry_ShouldReconcileAcrossAuthoritativeAndProjectionState()
    {
        var scenario = await ArrangeOpenBookAsync();
        var entry = await PostBalancedEntryAsync(scenario, ScenarioDefaults.FiscalYearStart.AddDays(2), 250m);

        var result = await Api.ReconcileJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);

        Assert.True(result.IsSuccess, result.RawBody);
        Assert.True(result.Value!.IsReconciled, result.RawBody);
        Assert.Empty(result.Value.Mismatches);
        await Assertions.AssertEntryMatchesAuthoritativeStateAsync(entry);
    }

    [Fact]
    [Trait("ScenarioId", "PROJ-009")]
    public async Task CorruptedTurnoverProjection_ShouldBeDetected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        await PostBalancedEntryAsync(scenario, date, 250m);
        await Inspector.CorruptTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date,
            "ASSET", "01", "01", null, ScenarioDefaults.DocumentTypeGeneral, 999m, 0m);

        var result = await Api.ReconcileJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);

        Assert.True(result.IsSuccess, result.RawBody);
        Assert.False(result.Value!.IsReconciled);
        Assert.Contains(result.Value.Mismatches, mismatch => mismatch.Projection == "TURNOVER");
    }

    [Fact]
    [Trait("ScenarioId", "PROJ-010")]
    public async Task CorruptedBalanceProjection_ShouldBeDetected()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        await PostBalancedEntryAsync(scenario, date, 250m);
        await Inspector.CorruptBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date,
            "ASSET", "01", "01", null, 999m);

        var result = await Api.ReconcileJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);

        Assert.True(result.IsSuccess, result.RawBody);
        Assert.False(result.Value!.IsReconciled);
        Assert.Contains(result.Value.Mismatches, mismatch => mismatch.Projection == "BALANCE");
    }

    [Fact]
    [Trait("ScenarioId", "PROJ-011")]
    [Trait("ScenarioId", "PROJ-012")]
    public async Task Rebuild_ShouldRepairCorruptedProjectionsWithoutChangingJournalSource()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        var entry = await PostBalancedEntryAsync(scenario, date, 250m);
        var beforeHeader = await Inspector.GetHeaderByReferenceAsync(
            scenario.Context.FiscalYearId, entry.ReferenceNumber);
        var beforeLines = await Inspector.GetOrderedLinesAsync(entry.Id);

        await Inspector.CorruptTurnoverAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date,
            "ASSET", "01", "01", null, ScenarioDefaults.DocumentTypeGeneral, 999m, 0m);
        await Inspector.CorruptBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date,
            "ASSET", "01", "01", null, 999m);

        var rebuild = await Api.RebuildJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);
        var reconcile = await Api.ReconcileJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);
        var afterHeader = await Inspector.GetHeaderByReferenceAsync(
            scenario.Context.FiscalYearId, entry.ReferenceNumber);
        var afterLines = await Inspector.GetOrderedLinesAsync(entry.Id);

        Assert.True(rebuild.IsSuccess, rebuild.RawBody);
        Assert.True(reconcile.IsSuccess, reconcile.RawBody);
        Assert.True(reconcile.Value!.IsReconciled, reconcile.RawBody);
        Assert.Equal(beforeHeader, afterHeader);
        Assert.Equal(beforeLines, afterLines);
    }

    [Fact]
    [Trait("ScenarioId", "PROJ-013")]
    public async Task Rebuild_ShouldBeDeterministicAndIdempotent()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        await PostBalancedEntryAsync(scenario, date, 250m);

        var first = await Api.RebuildJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);
        var firstRows = await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, "ASSET", "01", "01");
        var firstBalances = await Inspector.GetBalanceRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01");
        var second = await Api.RebuildJournalEntryProjectionsAsync(scenario.Context.FiscalYearId);
        var secondRows = await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, date, "ASSET", "01", "01");
        var secondBalances = await Inspector.GetBalanceRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId, "ASSET", "01", "01");

        Assert.True(first.IsSuccess, first.RawBody);
        Assert.True(second.IsSuccess, second.RawBody);
        Assert.Equal(firstRows, secondRows);
        Assert.Equal(firstBalances, secondBalances);
    }

    private async Task<AccountingScenario> ArrangeOpenBookAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
        await scenario.ActivateBookAsync();
        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync(
            "ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync(
            "EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);
        await scenario.CreateFiscalYearAsync(
            "FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    private static async Task<JournalEntryDetailResponse> PostBalancedEntryAsync(
        AccountingScenario scenario, DateOnly date, decimal amount)
    {
        var draft = await scenario.CreateDraftEntryAsync(
            date, "Projection test", [
                Debit("ASSET", "01", "01", amount, "Cash"),
                Credit("EQUITY", "01", "01", amount, "Capital")
            ]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }
}
