using Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;
using Apex.IntegrationTests.Common;
using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.JournalEntries.Domain;
using Apex.Modules.Accounting.JournalEntries.UseCases;
using Apex.Modules.Accounting.JournalEntries.UseCases.CreateDraftJournalEntry;
using static Apex.IntegrationTests.Accounting.Scenarios.Infrastructure.AccountingScenario;

namespace Apex.IntegrationTests.Accounting.Scenarios;

public sealed class IdempotencyScenarios(ApexWebApplicationFactory factory)
    : AccountingScenarioTestBase(factory)
{
    [Fact]
    [Trait("ScenarioId", "IDEM-001")]
    public async Task TrustedSourceEntry_ShouldBeCreatedWithItsSourceIdentity()
    {
        var scenario = await ArrangeOpenBookAsync();

        var result = await scenario.CreateDraftEntryAsync(
            ScenarioDefaults.FiscalYearStart.AddDays(2), "System import", BalancedLines(100m),
            insertionType: ScenarioDefaults.InsertionTypeSystem,
            sourceType: "BANK_IMPORT", sourceReference: "BANK-IMPORT-001");

        Assert.True(result.IsSuccess, result.RawBody);
        Assert.Equal(ScenarioDefaults.InsertionTypeSystem, result.Value!.InsertionType);
        Assert.Equal("BANK_IMPORT", result.Value.SourceType);
        Assert.Equal("BANK-IMPORT-001", result.Value.SourceReference);
    }

    [Fact]
    [Trait("ScenarioId", "IDEM-002")]
    [Trait("ScenarioId", "IDEM-003")]
    public async Task RepeatingTheSameSourceRequest_ShouldReturnTheOriginalWithoutDuplicateProjectionMovement()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        var first = await scenario.CreateDraftEntryAsync(
            date, "System import", BalancedLines(100m),
            insertionType: ScenarioDefaults.InsertionTypeSystem,
            sourceType: "BANK_IMPORT", sourceReference: "BANK-IMPORT-002");
        Assert.True(first.IsSuccess, first.RawBody);
        var posted = await scenario.PostEntryAsync(first.Value!.ReferenceNumber);
        Assert.True(posted.IsSuccess, posted.RawBody);

        var replay = await Api.CreateDraftEntryAsync(new CreateDraftJournalEntryRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountingDate = date,
            Description = "System import",
            DocumentType = ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = ScenarioDefaults.InsertionTypeSystem,
            BalanceEffect = ScenarioDefaults.BalanceEffectFinancial,
            SourceType = "BANK_IMPORT",
            SourceReference = "BANK-IMPORT-002",
            Lines = BalancedLines(100m)
        });

        Assert.True(replay.IsSuccess, replay.RawBody);
        Assert.Equal(first.Value.Id, replay.Value!.Id);
        Assert.Equal(1, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
        await Assertions.AssertClosingBalanceAsync(
            scenario.Context.BookId, scenario.Context.FiscalYearId,
            "ASSET", "01", "01", null, date, 100m);
    }

    [Fact]
    [Trait("ScenarioId", "IDEM-004")]
    public async Task SameSourceReferenceWithDifferentSourceType_ShouldRemainDistinct()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        var first = await scenario.CreateDraftEntryAsync(
            date, "First source", BalancedLines(100m),
            insertionType: ScenarioDefaults.InsertionTypeSystem,
            sourceType: "SOURCE_A", sourceReference: "SHARED-REFERENCE");
        var second = await scenario.CreateDraftEntryAsync(
            date, "Second source", BalancedLines(200m),
            insertionType: ScenarioDefaults.InsertionTypeSystem,
            sourceType: "SOURCE_B", sourceReference: "SHARED-REFERENCE");

        Assert.True(first.IsSuccess, first.RawBody);
        Assert.True(second.IsSuccess, second.RawBody);
        Assert.NotEqual(first.Value!.Id, second.Value!.Id);
        Assert.Equal(2, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
    }

    [Fact]
    [Trait("ScenarioId", "IDEM-008")]
    [Trait("ScenarioId", "CON-005")]
    public async Task ConcurrentIdenticalSourceRequests_ShouldCreateAtMostOneEntry()
    {
        var scenario = await ArrangeOpenBookAsync();
        var request = new CreateDraftJournalEntryRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountingDate = ScenarioDefaults.FiscalYearStart.AddDays(2),
            Description = "Concurrent import",
            DocumentType = ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = ScenarioDefaults.InsertionTypeSystem,
            BalanceEffect = ScenarioDefaults.BalanceEffectFinancial,
            SourceType = "CONCURRENT_IMPORT",
            SourceReference = "CONCURRENT-001",
            Lines = BalancedLines(100m)
        };
        var results = await RunTogetherAsync(
            Enumerable.Range(0, 6).Select(_ => (Func<Task<ScenarioApiResult<JournalEntryDetailResponse>>>)
                (() => Api.CreateDraftEntryAsync(request))));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.RawBody));
        Assert.Single(results.Select(result => result.Value!.Id).Distinct());
        Assert.Equal(1, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
    }

    [Fact]
    [Trait("ScenarioId", "IDEM-005")]
    public async Task SameSourcePairInADifferentFiscalYear_ShouldRemainIndependentlyEligible()
    {
        var scenario = await ArrangeOpenBookAsync();
        var firstYearDate = ScenarioDefaults.FiscalYearStart.AddDays(2);
        var first = await scenario.CreateDraftEntryAsync(
            firstYearDate, "First fiscal year import", BalancedLines(100m),
            insertionType: ScenarioDefaults.InsertionTypeSystem,
            sourceType: "BANK_IMPORT", sourceReference: "SHARED-ACROSS-YEARS");
        Assert.True(first.IsSuccess, first.RawBody);
        Assert.True((await scenario.PostEntryAsync(first.Value!.ReferenceNumber)).IsSuccess);

        // Move the first fiscal year out of Open (finalize then cancel at that exact boundary,
        // the same recipe FiscalYearControlScenarios uses) so a second fiscal year in the same
        // book can be opened — at most one Open fiscal year is allowed per book (FY-003).
        Assert.True((await Api.FinalizeFiscalYearAsync(
            scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart)).IsSuccess);
        var cancel = await Api.CancelFiscalYearAsync(scenario.Context.FiscalYearId, ScenarioDefaults.FiscalYearStart);
        Assert.True(cancel.IsSuccess, cancel.RawBody);

        var secondYear = await Api.CreateFiscalYearAsync(
            scenario.Context.BookId, "FY-2026-B",
            ScenarioDefaults.FiscalYearStart.AddDays(1), ScenarioDefaults.FiscalYearEnd);
        Assert.True(secondYear.IsSuccess, secondYear.RawBody);
        Assert.True((await Api.OpenFiscalYearAsync(secondYear.Value!.Id)).IsSuccess);

        // Same (Source Type, Source Reference) pair, different Fiscal Year: the unique index is
        // scoped to (fiscal_year_id, source_type, source_reference) — this must not collide.
        var second = await Api.CreateDraftEntryAsync(new CreateDraftJournalEntryRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = secondYear.Value.Id,
            AccountingDate = ScenarioDefaults.FiscalYearStart.AddDays(3),
            Description = "Second fiscal year import",
            DocumentType = ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = ScenarioDefaults.InsertionTypeSystem,
            BalanceEffect = ScenarioDefaults.BalanceEffectFinancial,
            SourceType = "BANK_IMPORT",
            SourceReference = "SHARED-ACROSS-YEARS",
            Lines = BalancedLines(250m)
        });

        Assert.True(second.IsSuccess, second.RawBody);
        Assert.NotEqual(first.Value.Id, second.Value!.Id);
        Assert.Equal(1, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
        Assert.Equal(1, await Inspector.CountEntriesAsync(secondYear.Value.Id));
    }

    [Fact]
    [Trait("ScenarioId", "IDEM-006")]
    public async Task FailedFirstAttempt_ShouldNotPreventALaterValidAttemptWithTheSameSource()
    {
        var scenario = await ArrangeOpenBookAsync();
        var date = ScenarioDefaults.FiscalYearStart.AddDays(2);
        const string sourceType = "BATCH_IMPORT";
        const string sourceReference = "IDEM-006-RETRY";

        // The first attempt is rejected by line validation (negative amount) before any row is
        // ever written — no committed idempotency record exists for this source pair yet.
        var failedAttempt = await Api.CreateDraftEntryAsync(new CreateDraftJournalEntryRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountingDate = date,
            Description = "Invalid retry attempt",
            DocumentType = ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = ScenarioDefaults.InsertionTypeSystem,
            BalanceEffect = ScenarioDefaults.BalanceEffectFinancial,
            SourceType = sourceType,
            SourceReference = sourceReference,
            Lines = [Debit("ASSET", "01", "01", -50m, "Invalid negative amount"), Credit("EQUITY", "01", "01", 50m, "Capital")]
        });
        Assert.False(failedAttempt.IsSuccess);
        Assert.Equal(0, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));

        // A later valid attempt with the exact same source pair must succeed normally.
        var validAttempt = await Api.CreateDraftEntryAsync(new CreateDraftJournalEntryRequest
        {
            AccountingBookId = scenario.Context.BookId,
            FiscalYearId = scenario.Context.FiscalYearId,
            AccountingDate = date,
            Description = "Corrected retry attempt",
            DocumentType = ScenarioDefaults.DocumentTypeGeneral,
            InsertionType = ScenarioDefaults.InsertionTypeSystem,
            BalanceEffect = ScenarioDefaults.BalanceEffectFinancial,
            SourceType = sourceType,
            SourceReference = sourceReference,
            Lines = BalancedLines(50m)
        });

        Assert.True(validAttempt.IsSuccess, validAttempt.RawBody);
        Assert.Equal(sourceReference, validAttempt.Value!.SourceReference);
        Assert.Equal(1, await Inspector.CountEntriesAsync(scenario.Context.FiscalYearId));
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
