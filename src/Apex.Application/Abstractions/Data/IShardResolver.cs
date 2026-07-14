namespace Apex.Application.Abstractions.Data;

public readonly record struct ShardKey
{
    public ShardKey(string entityType, string discriminator)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type is required.", nameof(entityType));

        if (string.IsNullOrWhiteSpace(discriminator))
            throw new ArgumentException("Shard discriminator is required.", nameof(discriminator));

        EntityType = entityType.Trim();
        Discriminator = discriminator.Trim();
    }

    public string EntityType { get; }
    public string Discriminator { get; }
}

public sealed record ShardLocation(
    string ShardId,
    string ConnectionName,
    string SchemaVersion,
    byte[] AssignmentVersion);

public interface IShardResolver
{
    Task<ShardLocation> ResolveAsync(
        ShardKey shardKey,
        CancellationToken cancellationToken = default);

    void Invalidate(ShardKey shardKey);
}

public interface IShardKeyFactory<in TPartition>
{
    ShardKey Create(TPartition partition);
}
