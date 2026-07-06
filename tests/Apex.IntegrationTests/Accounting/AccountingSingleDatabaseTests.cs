namespace Apex.IntegrationTests.Accounting;

using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

public sealed class AccountingSingleDatabaseTests : ApexIntegrationTestBase
{
    public AccountingSingleDatabaseTests(ApexIntegrationTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ReadAndWriteFactories_Should_Observe_Same_Database_State()
    {
        await ResetAccountingDatabaseAsync();

        await using var scope = await CreateScopeAsync();

        var writeFactory = scope.Services.GetRequiredService<IWriteDbConnectionFactory>();
        var readFactory = scope.Services.GetRequiredService<IReadDbConnectionFactory>();

        await using var writeConnection = await writeFactory.OpenConnectionAsync("Accounting");

        await writeConnection.ExecuteAsync("""
            DELETE FROM db_marker;
            INSERT INTO db_marker(name) VALUES ('SAME_DATABASE');
            """);

        await using var readConnection = await readFactory.OpenConnectionAsync("Accounting");

        var marker = await readConnection.ExecuteScalarAsync<string>(
            "SELECT TOP 1 name FROM db_marker");

        Assert.Equal("SAME_DATABASE", marker);
    }
}
