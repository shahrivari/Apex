using Apex.DatabaseMigrator.Migrations;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Apex.IntegrationTests.Http;

public class ApexWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string AccountingConnectionString => _sqlContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        RunMigrations();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task ResetAccountingDatabaseAsync()
    {
        await using var connection = new SqlConnection(AccountingConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(@"
            IF OBJECT_ID('accounting_book', 'U') IS NOT NULL
                DELETE FROM accounting_book;
        ");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:Accounting:Database:ReadConnectionStringName"] = "AccountingReadDb",
                ["Modules:Accounting:Database:WriteConnectionStringName"] = "AccountingWriteDb",
                ["Modules:Accounting:Database:Sharding:Enabled"] = "true",
                ["Modules:Accounting:Database:Sharding:Strategy"] = "FiscalYear",
                ["Modules:Accounting:Database:Sharding:DefaultShard"] = "Current",
                ["ConnectionStrings:AccountingReadDb"] = AccountingConnectionString,
                ["ConnectionStrings:AccountingWriteDb"] = AccountingConnectionString
            });
        });
    }

    void RunMigrations()
    {
        var result = DatabaseMigrationRunner.RunAccountingMigrations(AccountingConnectionString);
        if (!result.Successful)
            throw new InvalidOperationException("Database migration failed.", result.Error);
    }
}
