using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

public sealed class MultiFiscalYearScenarios : AccountingScenarioTestBase
{
    private readonly ApexWebApplicationFactory _factory;

    public MultiFiscalYearScenarios(ApexWebApplicationFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("ScenarioId", "MULTI-004")]
    [Trait("ScenarioId", "MULTI-005")]
    [Trait("ScenarioId", "MULTI-009")]
    public async Task CrossFiscalYearTurnover_ShouldAggregateOnlyTheRequestedPhysicalPartitions()
    {
        var prepared = await PrepareTwoShardFiscalYearsAsync();
        var report = await Api.GetCrossFiscalYearTurnoverAsync(
            prepared.Scenario.Context.BookId,
            [prepared.FirstFiscalYearId, prepared.SecondFiscalYearId],
            prepared.FirstDate, prepared.SecondDate);

        Assert.True(report.IsSuccess, report.RawBody);
        var asset = Assert.Single(report.Value!, item => item.AccountClassCode == "ASSET");
        Assert.Equal(140m, asset.DebitTurnover);
        Assert.Equal(140m, asset.NetTurnover);

        var firstOnly = await Api.GetCrossFiscalYearTurnoverAsync(
            prepared.Scenario.Context.BookId, [prepared.FirstFiscalYearId],
            prepared.FirstDate, prepared.FirstDate);
        var secondOnly = await Api.GetCrossFiscalYearTurnoverAsync(
            prepared.Scenario.Context.BookId, [prepared.SecondFiscalYearId],
            prepared.SecondDate, prepared.SecondDate);
        Assert.Equal(100m, Assert.Single(firstOnly.Value!, item => item.AccountClassCode == "ASSET").DebitTurnover);
        Assert.Equal(40m, Assert.Single(secondOnly.Value!, item => item.AccountClassCode == "ASSET").DebitTurnover);

        var shardOne = new ScenarioDatabaseInspector(_factory.ShardConnectionString);
        var shardTwo = new ScenarioDatabaseInspector(_factory.ShardTwoConnectionString);
        Assert.Equal("SHARD_ONE", await shardOne.GetShardMarkerAsync());
        Assert.Equal("SHARD_TWO", await shardTwo.GetShardMarkerAsync());
        Assert.NotNull(await shardOne.GetHeaderByReferenceAsync(prepared.FirstFiscalYearId, 1));
        Assert.NotNull(await shardTwo.GetHeaderByReferenceAsync(prepared.SecondFiscalYearId, 1));

        var firstAssignment = await prepared.Directory.GetFiscalYearAssignmentAsync(prepared.FirstFiscalYearId);
        var secondAssignment = await prepared.Directory.GetFiscalYearAssignmentAsync(prepared.SecondFiscalYearId);
        Assert.Equal("shard-accounting-01", firstAssignment!.ShardId);
        Assert.Equal("shard-accounting-02", secondAssignment!.ShardId);
    }

    [Fact]
    [Trait("ScenarioId", "MULTI-006")]
    public async Task ReversalRequestThroughAnotherFiscalYear_ShouldNotCrossTheShardBoundary()
    {
        var prepared = await PrepareTwoShardFiscalYearsAsync(additionalFirstYearEntry: true);

        var result = await Api.ReverseEntryAsync(
            prepared.SecondFiscalYearId, prepared.FirstOnlyReference, prepared.SecondDate, "Wrong Fiscal Year");

        ScenarioAssertions.AssertRejected(
            result, HttpStatusCode.NotFound, JournalEntryErrors.NotFound);
        var original = await new ScenarioDatabaseInspector(_factory.ShardConnectionString)
            .GetHeaderByReferenceAsync(prepared.FirstFiscalYearId, prepared.FirstOnlyReference);
        Assert.Null(original!.ReversedByReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "MULTI-007")]
    [Trait("ScenarioId", "MULTI-008")]
    public async Task CrossFiscalYearReport_ShouldFailClosedWhenARequiredShardIsUnavailable()
    {
        var prepared = await PrepareTwoShardFiscalYearsAsync();
        await prepared.Directory.SetShardStatusAsync(
            ApexWebApplicationFactory.ShardTwoConnectionName, "SUSPENDED");
        _factory.InvalidateFiscalYearRouting(prepared.SecondFiscalYearId);

        try
        {
            var result = await Api.GetCrossFiscalYearTurnoverAsync(
                prepared.Scenario.Context.BookId,
                [prepared.FirstFiscalYearId, prepared.SecondFiscalYearId],
                prepared.FirstDate, prepared.SecondDate);

            ScenarioAssertions.AssertRejected(result, HttpStatusCode.ServiceUnavailable, "shard_unavailable");
            Assert.Null(result.Value);
        }
        finally
        {
            await prepared.Directory.SetShardStatusAsync(
                ApexWebApplicationFactory.ShardTwoConnectionName, "ACTIVE");
        }
    }

    [Fact]
    [Trait("ScenarioId", "MULTI-010")]
    public async Task RepairingTheFiscalYearDirectory_ShouldRestoreDiscoveryWithoutChangingShardData()
    {
        var prepared = await PrepareTwoShardFiscalYearsAsync();
        var shardTwo = new ScenarioDatabaseInspector(_factory.ShardTwoConnectionString);
        var before = await shardTwo.GetHeaderByReferenceAsync(prepared.SecondFiscalYearId, 1);
        await prepared.Directory.DeleteFiscalYearDirectoryRowAsync(prepared.SecondFiscalYearId);

        var missing = await Api.ResolveFiscalYearAsync(
            prepared.Scenario.Context.BookId, prepared.SecondDate);
        ScenarioAssertions.AssertRejected(missing, HttpStatusCode.NotFound, "fiscal_year_not_found_for_date");

        var repair = await Api.RepairFiscalYearDirectoryAsync(prepared.SecondFiscalYearId);
        var resolved = await Api.ResolveFiscalYearAsync(
            prepared.Scenario.Context.BookId, prepared.SecondDate);
        var after = await shardTwo.GetHeaderByReferenceAsync(prepared.SecondFiscalYearId, 1);

        Assert.True(repair.IsSuccess, repair.RawBody);
        Assert.True(resolved.IsSuccess, resolved.RawBody);
        Assert.Equal(prepared.SecondFiscalYearId, resolved.Value!.Id);
        Assert.Equal(before, after);
    }

    private async Task<PreparedMultiShardScenario> PrepareTwoShardFiscalYearsAsync(
        bool additionalFirstYearEntry = false)
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync(title: "Multi-shard scenario book");
        await scenario.ActivateBookAsync();
        await scenario.CreateAccountClassAsync("ASSET", "Assets");
        await scenario.CreateGeneralAccountAsync("ASSET", "01", "Cash", AccountNature.Debtor);
        await scenario.CreateSubsidiaryAccountAsync(
            "ASSET", "01", "01", "Cash", AccountNature.Debtor, DetailAccountType.None);
        await scenario.CreateAccountClassAsync("EQUITY", "Equity");
        await scenario.CreateGeneralAccountAsync("EQUITY", "01", "Capital", AccountNature.Creditor);
        await scenario.CreateSubsidiaryAccountAsync(
            "EQUITY", "01", "01", "Capital", AccountNature.Creditor, DetailAccountType.None);

        var firstDate = new DateOnly(2026, 1, 1);
        var secondDate = new DateOnly(2026, 1, 2);
        await scenario.CreateFiscalYearAsync("FY-SHARD-ONE", firstDate, firstDate);
        await scenario.OpenFiscalYearAsync();
        var firstFiscalYearId = scenario.Context.FiscalYearId;
        var first = await CreateAndPostEntryAsync(scenario, firstDate, 100m);
        var firstOnlyReference = first.ReferenceNumber;
        if (additionalFirstYearEntry)
        {
            var secondFirstYearEntry = await CreateAndPostEntryAsync(scenario, firstDate, 50m);
            firstOnlyReference = secondFirstYearEntry.ReferenceNumber;
        }
        var finalized = await Api.FinalizeFiscalYearAsync(firstFiscalYearId, firstDate);
        Assert.True(finalized.IsSuccess, finalized.RawBody);
        var cancelled = await Api.CancelFiscalYearAsync(firstFiscalYearId, firstDate);
        Assert.True(cancelled.IsSuccess, cancelled.RawBody);

        _factory.SelectShardForNextFiscalYear(ApexWebApplicationFactory.ShardTwoConnectionName);
        await scenario.CreateFiscalYearAsync("FY-SHARD-TWO", secondDate, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        var secondFiscalYearId = scenario.Context.FiscalYearId;
        var second = await CreateAndPostEntryAsync(scenario, secondDate, 40m);
        Assert.Equal(1, first.ReferenceNumber);
        Assert.Equal(1, second.ReferenceNumber);

        return new PreparedMultiShardScenario(
            scenario, firstFiscalYearId, secondFiscalYearId, firstDate, secondDate, firstOnlyReference,
            new ScenarioDirectoryInspector(_factory.AccountingConnectionString));
    }

    private static async Task<JournalEntryDetailResponse> CreateAndPostEntryAsync(
        AccountingScenario scenario, DateOnly date, decimal amount)
    {
        var draft = await scenario.CreateDraftEntryAsync(
            date, "Multi-shard movement", [
                Debit("ASSET", "01", "01", amount, "Cash"),
                Credit("EQUITY", "01", "01", amount, "Capital")
            ]);
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);
        return posted.Value!;
    }

    private sealed record PreparedMultiShardScenario(
        AccountingScenario Scenario, long FirstFiscalYearId, long SecondFiscalYearId,
        DateOnly FirstDate, DateOnly SecondDate, long FirstOnlyReference,
        ScenarioDirectoryInspector Directory);
}
