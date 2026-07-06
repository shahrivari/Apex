namespace Apex.IntegrationTests.Infrastructure.Data;

using Apex.DatabaseMigrator.Migrations;
using Apex.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

public sealed class SeparatedReadWriteIntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _accountingReadDb =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly MsSqlContainer _accountingWriteDb = 
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 

    public string AccountingReadConnectionString => _accountingReadDb.GetConnectionString();

    public string AccountingWriteConnectionString => _accountingWriteDb.GetConnectionString();

    public IConfiguration Configuration { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _accountingReadDb.StartAsync();
        await _accountingWriteDb.StartAsync();

        Configuration = BuildConfiguration();

        RunMigrations(AccountingReadConnectionString);
        RunMigrations(AccountingWriteConnectionString);

        await SeedDatabaseMarkersAsync();
    }

    public async Task DisposeAsync()
    {
        await _accountingReadDb.DisposeAsync();
        await _accountingWriteDb.DisposeAsync();
    }

    public ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Configuration);
        services.AddInfrastructure(Configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    public SqlConnection CreateAccountingReadConnection()
    {
        return new SqlConnection(AccountingReadConnectionString);
    }

    public SqlConnection CreateAccountingWriteConnection()
    {
        return new SqlConnection(AccountingWriteConnectionString);
    }

    public async Task ResetAccountingWriteDatabaseAsync()
    {
        await using var connection = CreateAccountingWriteConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync("""
            IF OBJECT_ID('write_transaction_test', 'U') IS NOT NULL
                DELETE FROM write_transaction_test;
            """);
    }

    private IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Modules:Accounting:Database:ReadConnectionStringName"] = "AccountingReadDb",
            ["Modules:Accounting:Database:WriteConnectionStringName"] = "AccountingWriteDb",
            ["Modules:Accounting:Database:Sharding:Enabled"] = "true",
            ["Modules:Accounting:Database:Sharding:Strategy"] = "FiscalYear",
            ["Modules:Accounting:Database:Sharding:DefaultShard"] = "Current",

            // Important: infra routing tests use two physical DBs.
            ["ConnectionStrings:AccountingReadDb"] = AccountingReadConnectionString,
            ["ConnectionStrings:AccountingWriteDb"] = AccountingWriteConnectionString
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void RunMigrations(string connectionString)
    {
        var result = DatabaseMigrationRunner.RunAccountingMigrations(connectionString);

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                "Database migration failed.",
                result.Error);
        }

        result = DatabaseMigrationRunner.RunTestMigrations(connectionString);

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                "Test migration failed.",
                result.Error);
        }
    }

    private async Task SeedDatabaseMarkersAsync()
    {
        await using var readConnection = CreateAccountingReadConnection();
        await using var writeConnection = CreateAccountingWriteConnection();

        await readConnection.OpenAsync();
        await writeConnection.OpenAsync();

        await readConnection.ExecuteAsync("""
            DELETE FROM db_marker;
            INSERT INTO db_marker(name) VALUES ('READ_DATABASE');
            """);

        await writeConnection.ExecuteAsync("""
            DELETE FROM db_marker;
            INSERT INTO db_marker(name) VALUES ('WRITE_DATABASE');
            """);
    }
}
