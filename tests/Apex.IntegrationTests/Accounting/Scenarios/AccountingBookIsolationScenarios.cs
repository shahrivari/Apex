using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

public sealed class AccountingBookIsolationScenarios(ApexWebApplicationFactory factory)
    : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "MULTI-001")]
    [Trait("ScenarioId", "MULTI-002")]
    public async Task IdenticalAccountCodesAcrossBooks_ShouldHaveIsolatedBalancesAndProjections()
    {
        var first = await ArrangeOpenBookAsync("First book");
        var second = await ArrangeOpenBookUsingExistingChartAsync("Second book");
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);

        await PostBalancedEntryAsync(first, date, 300m);
        await PostBalancedEntryAsync(second, date, 700m);

        await Assertions.AssertClosingBalanceAsync(
            first.Context.BookId, first.Context.FiscalYearId, "ASSET", "01", "01", null, date, 300m);
        await Assertions.AssertClosingBalanceAsync(
            second.Context.BookId, second.Context.FiscalYearId, "ASSET", "01", "01", null, date, 700m);
        var firstRows = await Inspector.GetTurnoverRowsAsync(
            first.Context.BookId, first.Context.FiscalYearId, date, "ASSET", "01", "01");
        var secondRows = await Inspector.GetTurnoverRowsAsync(
            second.Context.BookId, second.Context.FiscalYearId, date, "ASSET", "01", "01");
        Assert.Equal(300m, firstRows.Single(row => row.DocumentType == ScenarioDefaults.DocumentTypeGeneral).DebitTurnover);
        Assert.Equal(700m, secondRows.Single(row => row.DocumentType == ScenarioDefaults.DocumentTypeGeneral).DebitTurnover);
    }

    [Fact]
    [Trait("ScenarioId", "MULTI-003")]
    public async Task NumberSequences_ShouldBeIndependentBetweenBooks()
    {
        var first = await ArrangeOpenBookAsync("First book");
        var second = await ArrangeOpenBookUsingExistingChartAsync("Second book");
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);

        var firstEntry = await CreateDraftEntryAsync(first, date, 100m);
        var secondEntry = await CreateDraftEntryAsync(second, date, 100m);

        Assert.Equal(1, firstEntry.ReferenceNumber);
        Assert.Equal(1, secondEntry.ReferenceNumber);
        Assert.Equal(1, firstEntry.JournalEntryNumber);
        Assert.Equal(1, secondEntry.JournalEntryNumber);
    }

    private async Task<AccountingScenario> ArrangeOpenBookAsync(string title)
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync(title: title);
        await scenario.ActivateBookAsync();
        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync(
            "ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.Person);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync(
            "EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.Person);
        await scenario.SeedStandardDetailAccountAsync();
        await scenario.CreateFiscalYearAsync(
            $"FY-{title}", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    private async Task<AccountingScenario> ArrangeOpenBookUsingExistingChartAsync(string title)
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync(title: title);
        await scenario.ActivateBookAsync();
        await scenario.CreateFiscalYearAsync(
            $"FY-{title}", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    private static async Task<JournalEntryDetailResponse> CreateDraftEntryAsync(
        AccountingScenario scenario, DateOnly date, decimal amount)
    {
        var result = await scenario.CreateDraftEntryAsync(
            date, "Isolation test", [
                Debit("ASSET", "01", "01", amount, "Cash"),
                Credit("EQUITY", "01", "01", amount, "Capital")
            ]);
        Assert.True(result.IsSuccess, result.RawBody);
        return result.Value!;
    }

    private static async Task PostBalancedEntryAsync(
        AccountingScenario scenario, DateOnly date, decimal amount)
    {
        var draft = await CreateDraftEntryAsync(scenario, date, amount);
        var result = await scenario.PostEntryAsync(draft.ReferenceNumber);
        Assert.True(result.IsSuccess, result.RawBody);
    }
}
