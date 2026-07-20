namespace Apex.Application.Abstractions.Data;

/// <summary>
/// Ensures a routable shard assignment exists for a shard key before a sharded operation runs.
/// This is a persistence-infrastructure concern: it writes to the shard directory, which module
/// business code must never touch directly. It is idempotent and commits independently of any
/// caller transaction so that subsequent routing observes the assignment.
/// </summary>
public interface IShardAssignmentProvisioner
{
    Task EnsureAssignedAsync(ShardKey shardKey, CancellationToken cancellationToken = default);
}
