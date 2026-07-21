using System.Net;
using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.FiscalYears.UseCases.FinalizeFiscalYear;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

public sealed class ConcurrencyScenarios(ApexWebApplicationFactory factory)
    : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "CON-001")]
    [Trait("ScenarioId", "CON-002")]
    [Trait("ScenarioId", "NUM-019")]
    public async Task ConcurrentDraftCreation_ShouldAllocateUniqueReferenceAndJournalEntryNumbers()
    {
        var scenario = await ArrangeOpenBookAsync();
        var requests = Enumerable.Range(0, 8).Select(index => new CreateDraftJournalEntryRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountingDate = ScenarioDefaults.FiscalYearStart.AddDays(2),
            Description = $"Concurrent entry {index}",
            DocumentType = ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = ScenarioDefaults.InsertionTypeManual,
            BalanceEffect = ScenarioDefaults.BalanceEffectFinancial,
            Lines = BalancedLines(100m)
        }).ToList();
        var results = await RunTogetherAsync(requests.Select(request =>
            (Func<Task<ScenarioApiResult<JournalEntryDetailResponse>>>)(
                () => Api.CreateDraftEntryAsync(request))));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.RawBody));
        Assert.Equal(8, results.Select(result => result.Value!.ReferenceNumber).Distinct().Count());
        Assert.Equal(8, results.Select(result => result.Value!.JournalEntryNumber).Distinct().Count());
        Assert.Equal(8, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
    }

    [Fact]
    [Trait("ScenarioId", "CON-003")]
    [Trait("ScenarioId", "CON-004")]
    [Trait("ScenarioId", "PROJ-005")]
    [Trait("ScenarioId", "PROJ-008")]
    [Trait("ScenarioId", "CON-008")]
    public async Task ConcurrentPostingToTheSameAccount_ShouldPreserveEveryProjectionMovement()
    {
        var scenario = await ArrangeOpenBookAsync();
        var drafts = new List<JournalEntryDetailResponse>();
        for (var index = 0; index < 6; index++)
        {
            var result = await scenario.CreateDraftEntryAsync(
                ScenarioDefaults.FiscalYearStart.AddDays(2), $"Post {index}", BalancedLines(100m));
            Assert.True(result.IsSuccess, result.RawBody);
            drafts.Add(result.Value!);
        }

        var results = await RunTogetherAsync(
            drafts.Select(draft => (Func<Task<ScenarioApiResult<JournalEntryDetailResponse>>>)(
                () => Api.PostEntryAsync(scenario.Context.FiscalYearId, draft.Id))));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.RawBody));
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            "ASSET", "01", "01", null, ScenarioDefaults.FiscalYearStart.AddDays(2), 600m);
        var turnover = await Inspector.GetTurnoverRowsAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            ScenarioDefaults.FiscalYearStart.AddDays(2), "ASSET", "01", "01");
        Assert.Equal(600m, turnover.Single(row => row.DocumentType == ScenarioDefaults.DocumentTypeGeneral).DebitTurnover);
    }

    [Fact]
    [Trait("ScenarioId", "CON-006")]
    public async Task ConcurrentReversalAttempts_ShouldCreateExactlyOneReversal()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        var draft = await scenario.CreateDraftEntryAsync(date, "Entry to be reversed concurrently", BalancedLines(100m));
        Assert.True(draft.IsSuccess, draft.RawBody);
        var posted = await scenario.PostEntryAsync(draft.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);

        var results = await RunTogetherAsync(
            Enumerable.Range(0, 5).Select(_ => (Func<Task<ScenarioApiResult<JournalEntryDetailResponse>>>)(
                () => Api.ReverseEntryAsync(
                    scenario.Context.FiscalYearId, posted.Value!.ReferenceNumber, date.AddDays(1), "Correction"))));

        var successes = results.Where(result => result.IsSuccess).ToList();
        var failures = results.Where(result => !result.IsSuccess).ToList();
        Assert.Single(successes);
        Assert.Equal(4, failures.Count);
        Assert.All(failures, result =>
            ScenarioAssertions.AssertRejected(result, HttpStatusCode.Conflict, JournalEntryErrors.AlreadyReversed));

        var header = await Inspector.GetHeaderByReferenceAsync(scenario.Context.FiscalYearId, posted.Value!.ReferenceNumber);
        Assert.NotNull(header!.ReversedByReferenceNumber);
        Assert.Equal(successes[0].Value!.ReferenceNumber, header.ReversedByReferenceNumber);
    }

    [Fact]
    [Trait("ScenarioId", "CON-007")]
    public async Task ConcurrentPostingAndFinalization_ShouldNeverLeavePartialOrInconsistentNumbering()
    {
        var scenario = await ArrangeOpenBookAsync();
        var boundaryDate = ScenarioDefaults.FiscalYearStart;
        Assert.True((await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, boundaryDate)).IsSuccess);

        var raceDate = boundaryDate.AddDays(1);
        var drafts = new List<JournalEntryDetailResponse>();
        for (var index = 0; index < 3; index++)
        {
            var draft = await scenario.CreateDraftEntryAsync(raceDate, $"Race entry {index}", BalancedLines(50m));
            Assert.True(draft.IsSuccess, draft.RawBody);
            drafts.Add(draft.Value!);
        }

        var postResults = new ScenarioApiResult<JournalEntryDetailResponse>?[drafts.Count];
        ScenarioApiResult<FinalizeFiscalYearResponse>? finalizeResult = null;

        var operations = drafts
            .Select((draft, index) => (Func<Task>)(async () =>
            {
                postResults[index] = await Api.PostEntryAsync(scenario.Context.FiscalYearId, draft.Id);
            }))
            .Append(async () =>
            {
                finalizeResult = await Api.FinalizeFiscalYearAsync(scenario.Context.FiscalYearId, raceDate);
            })
            .ToArray();
        await RunConcurrentlyAsync(operations);

        // Posting and finalization both lock the same fiscal_year row (spec §20), so the two
        // operations fully serialize relative to each other. The outcome is always one of exactly
        // two consistent states — never a partial mix of finalized and un-finalized numbering for
        // entries on the race date.
        var counters = await Inspector.GetFiscalYearCountersAsync(scenario.Context.FiscalYearId);
        if (finalizeResult!.IsSuccess)
        {
            Assert.Equal(raceDate, counters.FinalizedThroughDate);
            Assert.All(postResults, result => Assert.True(result!.IsSuccess, result.RawBody));
            foreach (var draft in drafts)
            {
                var header = await Inspector.GetHeaderByReferenceAsync(scenario.Context.FiscalYearId, draft.ReferenceNumber);
                Assert.Equal("POSTED", header!.Status);
                Assert.True(header.NumberFinalized);
            }
        }
        else
        {
            Assert.Equal(boundaryDate, counters.FinalizedThroughDate);
            ScenarioAssertions.AssertRejected(
                finalizeResult, HttpStatusCode.Conflict, JournalEntryErrors.DraftsBlockFinalization);
            foreach (var draft in drafts)
            {
                var header = await Inspector.GetHeaderByReferenceAsync(scenario.Context.FiscalYearId, draft.ReferenceNumber);
                if (header?.Status == "POSTED")
                    Assert.False(header.NumberFinalized);
            }
        }
    }

    private static async Task RunConcurrentlyAsync(params Func<Task>[] operations)
    {
        using var ready = new CountdownEvent(operations.Length);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = operations.Select(async operation =>
        {
            ready.Signal();
            await gate.Task;
            await operation();
        }).ToList();
        ready.Wait();
        gate.SetResult();
        await Task.WhenAll(tasks);
    }

    private async Task<AccountingScenario> ArrangeOpenBookAsync()
    {
        var scenario = await NewScenarioAsync();
        await scenario.CreateBookAsync();
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
            "FY-2026", ScenarioDefaults.FiscalYearStart, ScenarioDefaults.FiscalYearEnd);
        await scenario.OpenFiscalYearAsync();
        return scenario;
    }

    private static JournalEntryLineRequest[] BalancedLines(decimal amount) =>
        [Debit("ASSET", "01", "01", amount, "Cash"), Credit("EQUITY", "01", "01", amount, "Capital")];

    private static async Task<IReadOnlyList<T>> RunTogetherAsync<T>(IEnumerable<Func<Task<T>>> operations)
    {
        var operationList = operations.ToList();
        using var ready = new CountdownEvent(operationList.Count);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = operationList.Select(async operation =>
        {
            ready.Signal();
            await gate.Task;
            return await operation();
        }).ToList();
        ready.Wait();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }
}
