namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;

public interface IShardDirectory
{
    Task<ShardDirectoryEntry?> FindAsync(ShardKey key, CancellationToken cancellationToken);
}
