namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlShardDirectory : IShardDirectory
{
    private readonly IConfiguration _configuration;

    public SqlShardDirectory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ShardDirectoryEntry?> FindAsync(
        ShardKey key,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ShardDirectoryEntry>(new CommandDefinition(
            """
            SELECT
                a.shard_id AS ShardId,
                a.status AS AssignmentStatus,
                a.version AS AssignmentVersion,
                s.connection_name AS ConnectionName,
                s.status AS ShardStatus,
                s.schema_version AS SchemaVersion
            FROM ShardAssignments a
            INNER JOIN Shards s ON s.id = a.shard_id
            WHERE a.entity_type = @EntityType
              AND a.discriminator = @Discriminator
            """,
            new { key.EntityType, key.Discriminator },
            cancellationToken: cancellationToken));
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var options = _configuration.GetSection("Sharding").Get<ShardingOptions>() ?? new ShardingOptions();
        var connectionString = _configuration.GetConnectionString(options.GeneralConnectionStringName)
            ?? throw new InvalidOperationException(
                $"General connection string '{options.GeneralConnectionStringName}' is missing.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
