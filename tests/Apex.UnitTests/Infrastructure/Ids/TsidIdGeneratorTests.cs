namespace Apex.UnitTests.Infrastructure.Ids;

using Apex.Infrastructure.Ids;

public sealed class TsidIdGeneratorTests
{
    [Fact]
    public void NewId_Should_Return_Positive_Long()
    {
        var generator = new TsidIdGenerator();

        var id = generator.NewId();

        Assert.True(id > 0);
    }

    [Fact]
    public void NewId_Should_Return_Unique_Values()
    {
        var generator = new TsidIdGenerator();

        var ids = Enumerable.Range(0, 10_000)
            .Select(_ => generator.NewId())
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void NewId_Should_Be_Mostly_Increasing_For_Sequential_Generation()
    {
        var generator = new TsidIdGenerator();

        var previous = generator.NewId();

        for (var i = 0; i < 1_000; i++)
        {
            var current = generator.NewId();

            Assert.True(
                current > previous,
                $"Expected TSID to increase. Previous={previous}, Current={current}");

            previous = current;
        }
    }

    [Fact]
    public void NewId_Should_Work_When_Generated_In_Parallel()
    {
        var generator = new TsidIdGenerator();

        var ids = ParallelEnumerable.Range(0, 20_000)
            .Select(_ => generator.NewId())
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(id > 0));
    }
}
