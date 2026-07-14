namespace Apex.UnitTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Apex.Infrastructure.Data;
using Apex.Application.Abstractions.Time;
using Microsoft.Extensions.Configuration;
using NSubstitute;

public sealed class DefaultShardResolverTests
{
    private static readonly ShardKey Key = new("FiscalYear", "HAMI-1-2025");

    [Fact]
    public async Task ResolveAsync_Should_Return_Active_SchemaReady_Assignment()
    {
        var directory = Substitute.For<IShardDirectory>();
        directory.FindAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Entry("ACTIVE", "ACTIVE", "1"));
        var resolver = CreateResolver(directory);

        var result = await resolver.ResolveAsync(Key);

        Assert.Equal("shard-1", result.ShardId);
        Assert.Equal("ShardOne", result.ConnectionName);
    }

    [Fact]
    public async Task ResolveAsync_Should_Allow_Draining_Shard()
    {
        var directory = Substitute.For<IShardDirectory>();
        directory.FindAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Entry("ACTIVE", "DRAINING", "1"));
        var resolver = CreateResolver(directory);

        var result = await resolver.ResolveAsync(Key);

        Assert.Equal("shard-1", result.ShardId);
    }

    [Fact]
    public async Task ResolveAsync_Should_Reject_NonActive_Assignment()
    {
        var directory = Substitute.For<IShardDirectory>();
        directory.FindAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Entry("SUSPENDED", "ACTIVE", "1"));
        var resolver = CreateResolver(directory);

        await Assert.ThrowsAsync<ShardAssignmentNotFoundException>(() => resolver.ResolveAsync(Key));
    }

    [Fact]
    public async Task ResolveAsync_Should_Reject_Behind_Schema()
    {
        var directory = Substitute.For<IShardDirectory>();
        directory.FindAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Entry("ACTIVE", "ACTIVE", "0"));
        var resolver = CreateResolver(directory);

        await Assert.ThrowsAsync<ShardSchemaMismatchException>(() => resolver.ResolveAsync(Key));
    }

    [Fact]
    public async Task ResolveAsync_Should_Cache_And_Invalidate_Assignment()
    {
        var directory = Substitute.For<IShardDirectory>();
        directory.FindAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Entry("ACTIVE", "ACTIVE", "1"));
        var resolver = CreateResolver(directory);

        await resolver.ResolveAsync(Key);
        await resolver.ResolveAsync(Key);
        await directory.Received(1).FindAsync(Key, Arg.Any<CancellationToken>());

        resolver.Invalidate(Key);
        await resolver.ResolveAsync(Key);
        await directory.Received(2).FindAsync(Key, Arg.Any<CancellationToken>());
    }

    private static ShardDirectoryEntry Entry(
        string assignmentStatus,
        string shardStatus,
        string schemaVersion) =>
        new(
            "shard-1",
            assignmentStatus,
            [1],
            "ShardOne",
            shardStatus,
            schemaVersion);

    private static DefaultShardResolver CreateResolver(IShardDirectory directory)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return new DefaultShardResolver(directory, Configuration(), clock);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sharding:RequiredSchemaVersion"] = "1",
                ["Sharding:RoutingCacheTtlSeconds"] = "30",
                ["ConnectionStrings:ShardOne"] = "Server=.;Database=ShardOne"
            })
            .Build();
}
