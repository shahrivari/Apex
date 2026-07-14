namespace Apex.Application.Abstractions.Data;

public sealed class ShardingOptions
{
    public string GeneralConnectionStringName { get; init; } = "GeneralDb";
    public string RequiredSchemaVersion { get; init; } = "1";
    public int RoutingCacheTtlSeconds { get; init; } = 30;
    public int RoutingCacheMaxEntries { get; init; } = 10_000;
}
