using System.Security.Claims;
using System.Text.Encodings.Web;
using Apex.Application.Abstractions.Data;
using Apex.DatabaseMigrator.Migrations;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace Apex.IntegrationTests.Common;

public class ApexWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04"
    ).Build();

    public string AccountingConnectionString => _sqlContainer.GetConnectionString();

    public string ShardConnectionString { get; private set; } = null!;

    public string ShardTwoConnectionString { get; private set; } = null!;

    public const string ShardOneConnectionName = "AccountingShard01";
    public const string ShardTwoConnectionName = "AccountingShard02";

    private readonly object _nextShardLock = new();
    private string? _nextShardConnectionName;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        RunMigrations();
        ShardConnectionString = await CreateShardDatabaseAsync("ApexShardOne", "SHARD_ONE");
        ShardTwoConnectionString = await CreateShardDatabaseAsync("ApexShardTwo", "SHARD_TWO");
        await SeedShardCatalogAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public void SelectShardForNextFiscalYear(string connectionName)
    {
        if (connectionName is not (ShardOneConnectionName or ShardTwoConnectionName))
            throw new ArgumentException("Unknown Accounting test shard.", nameof(connectionName));

        lock (_nextShardLock)
        {
            if (_nextShardConnectionName is not null)
                throw new InvalidOperationException("A shard selection is already queued.");

            _nextShardConnectionName = connectionName;
        }
    }

    internal string ConsumeNextShardConnectionName()
    {
        lock (_nextShardLock)
        {
            var selected = _nextShardConnectionName ?? ShardOneConnectionName;
            _nextShardConnectionName = null;
            return selected;
        }
    }

    public void InvalidateFiscalYearRouting(long fiscalYearId)
    {
        Services.GetRequiredService<IShardResolver>().Invalidate(
            new ShardKey("FiscalYear", fiscalYearId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public async Task ResetAccountingDatabaseAsync()
    {
        await using var connection = new SqlConnection(AccountingConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"
            IF OBJECT_ID('fiscal_year_directory', 'U') IS NOT NULL
                DELETE FROM fiscal_year_directory;

            IF OBJECT_ID('detail_account_retired_code', 'U') IS NOT NULL
                DELETE FROM detail_account_retired_code;

            IF OBJECT_ID('detail_account', 'U') IS NOT NULL
                DELETE FROM detail_account;

            IF OBJECT_ID('subsidiary_account', 'U') IS NOT NULL
                DELETE FROM subsidiary_account;

            IF OBJECT_ID('general_account', 'U') IS NOT NULL
                DELETE FROM general_account;

            IF OBJECT_ID('account_class', 'U') IS NOT NULL
                DELETE FROM account_class;

            IF OBJECT_ID('accounting_book', 'U') IS NOT NULL
                DELETE FROM accounting_book;

            IF OBJECT_ID('ShardAssignments', 'U') IS NOT NULL
                DELETE FROM ShardAssignments WHERE entity_type = 'FiscalYear';
        "
        );
    }

    public Task ResetShardDatabaseAsync() => ResetShardDatabasesAsync();

    public async Task ResetShardDatabasesAsync()
    {
        await ResetShardDatabaseAsync(ShardConnectionString);
        await ResetShardDatabaseAsync(ShardTwoConnectionString);
    }

    private static async Task ResetShardDatabaseAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            @"
            IF OBJECT_ID('journal_entry_line', 'U') IS NOT NULL
                DELETE FROM journal_entry_line;

            IF OBJECT_ID('daily_account_turnover', 'U') IS NOT NULL
                DELETE FROM daily_account_turnover;

            IF OBJECT_ID('daily_account_balance', 'U') IS NOT NULL
                DELETE FROM daily_account_balance;

            IF OBJECT_ID('journal_entry', 'U') IS NOT NULL
                DELETE FROM journal_entry;

            IF OBJECT_ID('fiscal_year', 'U') IS NOT NULL
                DELETE FROM fiscal_year;
        "
        );
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Sharding:GeneralConnectionStringName"] = "GeneralDb",
                        ["Sharding:RequiredSchemaVersion"] = "1",
                        ["ConnectionStrings:GeneralDb"] = AccountingConnectionString,
                        [$"ConnectionStrings:{ShardOneConnectionName}"] = ShardConnectionString,
                        [$"ConnectionStrings:{ShardTwoConnectionName}"] = ShardTwoConnectionString,
                    }
                );
            }
        );

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IShardAssignmentProvisioner>();
            services.AddSingleton<IShardAssignmentProvisioner>(serviceProvider =>
                new TestShardAssignmentProvisioner(
                    AccountingConnectionString,
                    this,
                    serviceProvider.GetRequiredService<Apex.Application.Abstractions.Time.IClock>()));
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { }
                );
        });
    }

    void RunMigrations()
    {
        var result = DatabaseMigrationRunner.RunGeneralMigrations(AccountingConnectionString);
        if (!result.Successful)
            throw new InvalidOperationException("Database migration failed.", result.Error);
    }

    private async Task<string> CreateShardDatabaseAsync(string databaseName, string marker)
    {
        var builder = new SqlConnectionStringBuilder(AccountingConnectionString)
        {
            InitialCatalog = databaseName
        };

        await using (var connection = new SqlConnection(AccountingConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync($"CREATE DATABASE [{databaseName}]");
        }

        var connectionString = builder.ConnectionString;
        var result = DatabaseMigrationRunner.RunShardMigrations(connectionString);
        if (!result.Successful)
            throw new InvalidOperationException("Shard migration failed.", result.Error);

        await using var shard = new SqlConnection(connectionString);
        await shard.OpenAsync();
        await shard.ExecuteAsync("CREATE TABLE shard_marker (name VARCHAR(50) NOT NULL)");
        await shard.ExecuteAsync(
            "INSERT INTO shard_marker(name) VALUES (@Marker)", new { Marker = marker });
        return connectionString;
    }

    private async Task SeedShardCatalogAsync()
    {
        await using var connection = new SqlConnection(AccountingConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO Shards (id, connection_name, status, schema_version, created_at, modified_at)
            VALUES ('shard-accounting-01', 'AccountingShard01', 'ACTIVE', '1',
                    SYSUTCDATETIME(), SYSUTCDATETIME()),
                   ('shard-accounting-02', 'AccountingShard02', 'ACTIVE', '1',
                    SYSUTCDATETIME(), SYSUTCDATETIME())
            """);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthenticationScheme = "IntegrationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (
                Request.Headers.TryGetValue("X-Test-Unauthenticated", out var value)
                && string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase)
            )
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "integration-test-user")],
                AuthenticationScheme
            );
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
