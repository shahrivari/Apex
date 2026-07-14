namespace Apex.Application.Abstractions.Data;

public abstract class ShardResolutionException : InvalidOperationException
{
    protected ShardResolutionException(string message) : base(message) { }
}

public sealed class ShardAssignmentNotFoundException : ShardResolutionException
{
    public ShardAssignmentNotFoundException(ShardKey key)
        : base($"No active shard assignment exists for entity type '{key.EntityType}'.") { }
}

public sealed class ShardUnavailableException : ShardResolutionException
{
    public ShardUnavailableException(string message) : base(message) { }
}

public sealed class ShardSchemaMismatchException : ShardResolutionException
{
    public ShardSchemaMismatchException(string shardId, string actual, string required)
        : base($"Shard '{shardId}' has schema version '{actual}', but '{required}' is required.") { }
}
