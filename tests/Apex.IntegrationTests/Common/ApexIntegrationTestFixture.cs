namespace Apex.IntegrationTests.Common;

using Apex.Application.Abstractions.Data;
using Apex.DatabaseMigrator.Migrations;
using Apex.Infrastructure;
using Apex.Modules.Accounting;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

public sealed class ApexIntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _accountingDb = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04"
    ).Build();

    public string AccountingConnectionString => _accountingDb.GetConnectionString();
    public string ShardConnectionString { get; private set; } = null!;

    public IConfiguration Configuration { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _accountingDb.StartAsync();

        ShardConnectionString = await CreateShardDatabaseAsync();

        Configuration = BuildConfiguration();

        RunMigrations(AccountingConnectionString);
        await SeedShardCatalogAsync();
    }

    public async Task DisposeAsync()
    {
        await _accountingDb.DisposeAsync();
    }

    public ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Configuration);
        services.AddLogging();
        services.AddInfrastructure(Configuration);
        services.AddAccountingModule(Configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    public SqlConnection CreateAccountingConnection()
    {
        return new SqlConnection(AccountingConnectionString);
    }

    public SqlConnection CreateShardConnection()
    {
        return new SqlConnection(ShardConnectionString);
    }

    public async Task ResetShardDatabaseAsync()
    {
        await using var connection = CreateShardConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
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
            """);
    }

    public async Task ResetAccountingDatabaseAsync()
    {
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            """
            IF OBJECT_ID('write_transaction_test', 'U') IS NOT NULL
                DELETE FROM write_transaction_test;

            IF OBJECT_ID('db_marker', 'U') IS NOT NULL
            BEGIN
                DELETE FROM db_marker;
                INSERT INTO db_marker(name) VALUES ('ACCOUNTING_DATABASE');
            END

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
            """
        );
    }

    private IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Sharding:GeneralConnectionStringName"] = "GeneralDb",
            ["Sharding:RequiredSchemaVersion"] = "1",
            ["ConnectionStrings:GeneralDb"] = AccountingConnectionString,
            ["ConnectionStrings:ShardOne"] = ShardConnectionString,
            ["ConnectionStrings:AccountingShard01"] = ShardConnectionString,
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private async Task SeedShardCatalogAsync()
    {
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO Shards (id, connection_name, status, schema_version, created_at, modified_at)
            VALUES ('shard-accounting-01', 'AccountingShard01', 'ACTIVE', '1',
                    SYSUTCDATETIME(), SYSUTCDATETIME())
            """);
    }

    private static void RunMigrations(string connectionString)
    {
        var result = DatabaseMigrationRunner.RunGeneralMigrations(connectionString);

        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        result = DatabaseMigrationRunner.RunTestMigrations(connectionString);

        if (!result.Successful)
        {
            throw new InvalidOperationException("Test migration failed.", result.Error);
        }
    }

    private async Task<string> CreateShardDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(AccountingConnectionString);
        var databaseName = "ApexShardOne";

        await using (var connection = new SqlConnection(AccountingConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync($"CREATE DATABASE [{databaseName}]");
        }

        builder.InitialCatalog = databaseName;
        var connectionString = builder.ConnectionString;
        var result = DatabaseMigrationRunner.RunShardMigrations(connectionString);
        if (!result.Successful)
            throw new InvalidOperationException("Shard migration failed.", result.Error);

        await using var shard = new SqlConnection(connectionString);
        await shard.OpenAsync();
        await shard.ExecuteAsync("CREATE TABLE shard_marker (name VARCHAR(50) NOT NULL)");
        await shard.ExecuteAsync("INSERT INTO shard_marker(name) VALUES ('SHARD_ONE')");
        return connectionString;
    }
}
