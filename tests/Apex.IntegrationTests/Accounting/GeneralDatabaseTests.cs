namespace Apex.IntegrationTests.Accounting;

using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

public sealed class GeneralDatabaseTests : ApexIntegrationTestBase
{
    public GeneralDatabaseTests(ApexIntegrationTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GeneralFactory_Should_Use_GeneralDatabase()
    {
        await ResetAccountingDatabaseAsync();
        await using var scope = await CreateScopeAsync();
        var connectionFactory = scope.Services.GetRequiredService<IGeneralConnectionFactory>();

        var connection = await connectionFactory.OpenAsync();
        await connection.ExecuteAsync("""
            DELETE FROM db_marker;
            INSERT INTO db_marker(name) VALUES ('GENERAL_DATABASE');
            """);

        var marker = await connection.ExecuteScalarAsync<string>(
            "SELECT TOP 1 name FROM db_marker");

        Assert.Equal("GENERAL_DATABASE", marker);
    }
}
