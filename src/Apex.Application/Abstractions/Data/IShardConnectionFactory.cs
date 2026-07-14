namespace Apex.Application.Abstractions.Data;

using System.Data.Common;

public interface IShardConnection : IAsyncDisposable
{
    ShardLocation Location { get; }
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }
}

public interface IShardConnectionFactory
{
    Task<IShardConnection> OpenAsync(
        ShardKey shardKey,
        bool beginTransaction = false,
        CancellationToken cancellationToken = default);
}
