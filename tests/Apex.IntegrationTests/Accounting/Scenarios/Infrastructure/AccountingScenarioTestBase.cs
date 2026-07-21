using Apex.IntegrationTests.Common;
using Apex.IntegrationTests.Http;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Shared fixture wiring for every Accounting scenario test class: resets both databases before
/// each test (shard first, then general — spec §5.2) and exposes the typed
/// <see cref="ScenarioApiClient"/>, <see cref="ScenarioDatabaseInspector"/>, and
/// <see cref="ScenarioAssertions"/> collaborators plus a fresh-scenario helper. All scenario test
/// classes share the one <see cref="ApexWebApplicationFactory"/> collection fixture, so they must
/// not run concurrently with it (enforced by xUnit collection semantics).
/// </summary>
[Collection(ApexHttpIntegrationTestCollection.Name)]
public abstract class AccountingScenarioTestBase(ApexWebApplicationFactory factory) : IAsyncLifetime
{
    private HttpClient _client = null!;

    protected ScenarioApiClient Api { get; private set; } = null!;
    protected ScenarioDatabaseInspector Inspector { get; private set; } = null!;
    protected ScenarioAssertions Assertions { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Shard first, then general: journal_entry/fiscal_year live on the shard and must be
        // cleared before the general accounting_book/fiscal_year_directory rows that reference
        // them logically, keeping the two databases mutually consistent between tests.
        await factory.ResetShardDatabaseAsync();
        await factory.ResetAccountingDatabaseAsync();

        _client = factory.CreateClient();
        Api = new ScenarioApiClient(_client);
        Inspector = new ScenarioDatabaseInspector(factory.ShardConnectionString);
        Assertions = new ScenarioAssertions(Api, Inspector);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Starts a fresh, independent <see cref="AccountingScenario"/> for one test.</summary>
    protected Task<AccountingScenario> NewScenarioAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AccountingScenario.Start(Api));
}
