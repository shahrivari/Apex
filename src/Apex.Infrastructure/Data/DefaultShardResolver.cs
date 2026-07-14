namespace Apex.Infrastructure.Data;

using System.Collections.Concurrent;
using Apex.Application.Abstractions.Data;
using Apex.Application.Abstractions.Time;
using Microsoft.Extensions.Configuration;

public sealed class DefaultShardResolver : IShardResolver
{
    private sealed record CacheEntry(ShardLocation Location, DateTime ExpiresAt);

    private readonly IShardDirectory _directory;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<ShardKey, CacheEntry> _cache = new();

    public DefaultShardResolver(
        IShardDirectory directory,
        IConfiguration configuration,
        IClock clock)
    {
        _directory = directory;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task<ShardLocation> ResolveAsync(
        ShardKey shardKey,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        if (_cache.TryGetValue(shardKey, out var cached) && cached.ExpiresAt > now)
            return cached.Location;

        _cache.TryRemove(shardKey, out _);
        var entry = await _directory.FindAsync(shardKey, cancellationToken);

        if (entry is null || !string.Equals(entry.AssignmentStatus, "ACTIVE", StringComparison.Ordinal))
            throw new ShardAssignmentNotFoundException(shardKey);

        if (entry.ShardStatus is not ("ACTIVE" or "DRAINING"))
            throw new ShardUnavailableException(
                $"Shard '{entry.ShardId}' is not routable (status '{entry.ShardStatus}').");

        var options = GetOptions();
        var actualSchemaVersion = entry.SchemaVersion ?? string.Empty;
        if (!string.Equals(actualSchemaVersion, options.RequiredSchemaVersion, StringComparison.Ordinal))
            throw new ShardSchemaMismatchException(
                entry.ShardId, actualSchemaVersion, options.RequiredSchemaVersion);

        if (string.IsNullOrWhiteSpace(_configuration.GetConnectionString(entry.ConnectionName)))
            throw new ShardUnavailableException(
                $"Allow-listed connection '{entry.ConnectionName}' for shard '{entry.ShardId}' is missing.");

        var location = new ShardLocation(
            entry.ShardId,
            entry.ConnectionName,
            actualSchemaVersion,
            entry.AssignmentVersion);

        var ttl = TimeSpan.FromSeconds(Math.Max(1, options.RoutingCacheTtlSeconds));
        TrimCache(now, Math.Max(1, options.RoutingCacheMaxEntries));
        _cache[shardKey] = new CacheEntry(location, now.Add(ttl));
        return location;
    }

    public void Invalidate(ShardKey shardKey) => _cache.TryRemove(shardKey, out _);

    private void TrimCache(DateTime now, int maximumEntries)
    {
        foreach (var entry in _cache)
        {
            if (entry.Value.ExpiresAt <= now)
                _cache.TryRemove(entry.Key, out _);
        }

        while (_cache.Count >= maximumEntries)
        {
            var key = _cache.Keys.FirstOrDefault();
            if (!_cache.TryRemove(key, out _))
                break;
        }
    }

    private ShardingOptions GetOptions() =>
        _configuration.GetSection("Sharding").Get<ShardingOptions>() ?? new ShardingOptions();
}
