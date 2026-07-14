namespace Apex.Infrastructure.Data;

using System.Data.Common;
using Apex.Application.Abstractions.Data;

internal sealed class SqlShardConnection : IShardConnection
{
    public SqlShardConnection(
        ShardLocation location,
        DbConnection connection,
        DbTransaction? transaction)
    {
        Location = location;
        Connection = connection;
        Transaction = transaction;
    }

    public ShardLocation Location { get; }
    public DbConnection Connection { get; }
    public DbTransaction? Transaction { get; }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
            await Transaction.DisposeAsync();

        await Connection.DisposeAsync();
    }
}
