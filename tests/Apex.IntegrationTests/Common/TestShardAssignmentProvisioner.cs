using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Time;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Apex.IntegrationTests.Common;

internal sealed class TestShardAssignmentProvisioner(
    string generalConnectionString,
    ApexWebApplicationFactory factory,
    IClock clock) : IShardAssignmentProvisioner
{
    public async Task EnsureAssignedAsync(ShardKey shardKey, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(generalConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM ShardAssignments WITH (UPDLOCK, HOLDLOCK)
            WHERE entity_type = @EntityType AND discriminator = @Discriminator
            """,
            new { shardKey.EntityType, shardKey.Discriminator }, transaction,
            cancellationToken: cancellationToken));
        if (existing > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var connectionName = factory.ConsumeNextShardConnectionName();
        var shardId = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            """
            SELECT id
            FROM Shards WITH (UPDLOCK, HOLDLOCK)
            WHERE connection_name = @ConnectionName
              AND status = 'ACTIVE' AND schema_version = '1'
            """,
            new { ConnectionName = connectionName }, transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO ShardAssignments
                (entity_type, discriminator, shard_id, status, created_at, modified_at)
            VALUES (@EntityType, @Discriminator, @ShardId, 'ACTIVE', @Now, @Now)
            """,
            new
            {
                shardKey.EntityType, shardKey.Discriminator, ShardId = shardId,
                Now = clock.UtcNow
            }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }
}
