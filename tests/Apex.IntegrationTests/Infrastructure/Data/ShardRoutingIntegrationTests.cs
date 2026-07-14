namespace Apex.IntegrationTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Apex.IntegrationTests.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

public sealed class ShardRoutingIntegrationTests(ApexIntegrationTestFixture fixture)
    : ApexIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Factory_Opens_Assigned_Physical_Shard()
    {
        var key = new ShardKey("FiscalYear", "HAMI-1-2025");
        await using (var general = CreateAccountingConnection())
        {
            await general.OpenAsync();
            await general.ExecuteAsync(
                """
                DELETE FROM ShardAssignments WHERE entity_type = @EntityType AND discriminator = @Discriminator;
                IF NOT EXISTS (SELECT 1 FROM Shards WHERE id = 'shard-1')
                    INSERT INTO Shards (id, connection_name, status, schema_version, created_at, modified_at)
                    VALUES ('shard-1', 'ShardOne', 'ACTIVE', '1', SYSUTCDATETIME(), SYSUTCDATETIME());
                INSERT INTO ShardAssignments
                    (entity_type, discriminator, shard_id, status, created_at, modified_at)
                VALUES
                    (@EntityType, @Discriminator, 'shard-1', 'ACTIVE', SYSUTCDATETIME(), SYSUTCDATETIME());
                """,
                new { key.EntityType, key.Discriminator });
        }

        await using var scope = await CreateScopeAsync();
        var factory = scope.Services.GetRequiredService<IShardConnectionFactory>();
        await using var shard = await factory.OpenAsync(key);

        var marker = await shard.Connection.QuerySingleAsync<string>(
            "SELECT name FROM shard_marker");

        Assert.Equal("SHARD_ONE", marker);
        Assert.Equal("shard-1", shard.Location.ShardId);
    }
}
