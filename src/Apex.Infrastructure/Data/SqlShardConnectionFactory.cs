namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlShardConnectionFactory : IShardConnectionFactory
{
    private readonly IShardResolver _resolver;
    private readonly IConfiguration _configuration;

    public SqlShardConnectionFactory(IShardResolver resolver, IConfiguration configuration)
    {
        _resolver = resolver;
        _configuration = configuration;
    }

    public async Task<IShardConnection> OpenAsync(
        ShardKey shardKey,
        bool beginTransaction = false,
        CancellationToken cancellationToken = default)
    {
        var location = await _resolver.ResolveAsync(shardKey, cancellationToken);
        return await OpenLocationAsync(location, beginTransaction, cancellationToken);
    }

    internal async Task<IShardConnection> OpenLocationAsync(
        ShardLocation location,
        bool beginTransaction,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(location.ConnectionName)
            ?? throw new ShardUnavailableException(
                $"Allow-listed connection '{location.ConnectionName}' for shard '{location.ShardId}' is missing.");

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = beginTransaction
                ? await connection.BeginTransactionAsync(cancellationToken)
                : null;

            return new SqlShardConnection(location, connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
