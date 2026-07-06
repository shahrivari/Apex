namespace Apex.IntegrationTests.Common;

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
    private readonly MsSqlContainer _accountingDb = 
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    
    public string AccountingConnectionString => _accountingDb.GetConnectionString();

    public IConfiguration Configuration { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _accountingDb.StartAsync();

        Configuration = BuildConfiguration();

        RunMigrations(AccountingConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _accountingDb.DisposeAsync();
    }

    public ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Configuration);
        services.AddInfrastructure(Configuration);
        services.AddAccountingModule(Configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    public SqlConnection CreateAccountingConnection()
    {
        return new SqlConnection(AccountingConnectionString);
    }

    public async Task ResetAccountingDatabaseAsync()
    {
        await using var connection = CreateAccountingConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync("""
            IF OBJECT_ID('write_transaction_test', 'U') IS NOT NULL
                DELETE FROM write_transaction_test;

            IF OBJECT_ID('db_marker', 'U') IS NOT NULL
            BEGIN
                DELETE FROM db_marker;
                INSERT INTO db_marker(name) VALUES ('ACCOUNTING_DATABASE');
            END

            IF OBJECT_ID('accounting_book', 'U') IS NOT NULL
                DELETE FROM accounting_book;
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

            // Important: normal business tests use one physical DB.
            ["ConnectionStrings:AccountingReadDb"] = AccountingConnectionString,
            ["ConnectionStrings:AccountingWriteDb"] = AccountingConnectionString
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
}
