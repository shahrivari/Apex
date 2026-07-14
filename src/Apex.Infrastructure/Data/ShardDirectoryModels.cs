namespace Apex.Infrastructure.Data;

public sealed record ShardDirectoryEntry(
    string ShardId,
    string AssignmentStatus,
    byte[] AssignmentVersion,
    string ConnectionName,
    string ShardStatus,
    string? SchemaVersion);
