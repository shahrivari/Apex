namespace Apex.UnitTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;

public sealed class ShardKeyTests
{
    [Fact]
    public void Constructor_Should_Use_EntityType_And_Normalized_Discriminator()
    {
        var key = new ShardKey(" FiscalYear ", " HAMI-1-2025 ");

        Assert.Equal("FiscalYear", key.EntityType);
        Assert.Equal("HAMI-1-2025", key.Discriminator);
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("FiscalYear", "")]
    [InlineData(" ", "value")]
    [InlineData("FiscalYear", " ")]
    public void Constructor_Should_Reject_Empty_Components(
        string entityType,
        string discriminator)
    {
        Assert.Throws<ArgumentException>(() => new ShardKey(entityType, discriminator));
    }
}
