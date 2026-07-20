namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Time;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Idempotently assigns a shard key to a routable shard in the General Database directory.
/// Because per-partition assignments use runtime-generated discriminators (e.g. fiscal-year ids)
/// they cannot be seeded by a static migration; this provisioner creates the assignment on first
/// use by mapping the key to the lowest-id ACTIVE shard. A formal, balanced provisioning workflow
/// is a future concern; this keeps single-shard deployments working end-to-end.
/// </summary>
public sealed class SqlShardAssignmentProvisioner : IShardAssignmentProvisioner
{
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public SqlShardAssignmentProvisioner(IConfiguration configuration, IClock clock)
    {
        _configuration = configuration;
        _clock = clock;
    }

    public async Task EnsureAssignedAsync(ShardKey shardKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var now = _clock.UtcNow;
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

        var options = _configuration.GetSection("Sharding").Get<ShardingOptions>() ?? new ShardingOptions();
        var candidates = (await connection.QueryAsync<ShardCandidate>(new CommandDefinition(
            """
            SELECT id AS Id, connection_name AS ConnectionName
            FROM Shards WITH (UPDLOCK, HOLDLOCK)
            WHERE status = 'ACTIVE' AND schema_version = @RequiredSchemaVersion
            ORDER BY id
            """,
            new { options.RequiredSchemaVersion }, transaction,
            cancellationToken: cancellationToken))).AsList();
        var candidate = candidates.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(_configuration.GetConnectionString(item.ConnectionName)))
            ?? throw new ShardUnavailableException(
                "No active, configured shard at the required schema version is available.");

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO ShardAssignments
                (entity_type, discriminator, shard_id, status, created_at, modified_at)
            VALUES (@EntityType, @Discriminator, @ShardId, 'ACTIVE', @Now, @Now)
            """,
            new
            {
                shardKey.EntityType,
                shardKey.Discriminator,
                ShardId = candidate.Id,
                Now = now
            }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
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

    private sealed record ShardCandidate(string Id, string ConnectionName);
}
