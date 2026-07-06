namespace Apex.UnitTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Apex.Infrastructure.Data;
using NSubstitute;

public sealed class DefaultShardResolverTests
{
    [Fact]
    public void ResolveTableName_Should_Return_Logical_Name_When_Sharding_Disabled()
    {
        var moduleDbResolver = Substitute.For<IModuleDatabaseResolver>();
        moduleDbResolver.GetShardingConfig("Accounting")
            .Returns(new ModuleShardingConfig
            {
                Enabled = false,
                Strategy = "None"
            });

        var resolver = new DefaultShardResolver(moduleDbResolver);

        var tableName = resolver.ResolveTableName(
            "Accounting",
            "accounting_journal_lines",
            new ShardContext(FiscalYear: 1405));

        Assert.Equal("accounting_journal_lines", tableName);
    }

    [Fact]
    public void ResolveTableName_Should_Append_FiscalYear_When_Strategy_Is_FiscalYear()
    {
        var moduleDbResolver = Substitute.For<IModuleDatabaseResolver>();
        moduleDbResolver.GetShardingConfig("Accounting")
            .Returns(new ModuleShardingConfig
            {
                Enabled = true,
                Strategy = "FiscalYear"
            });

        var resolver = new DefaultShardResolver(moduleDbResolver);

        var tableName = resolver.ResolveTableName(
            "Accounting",
            "accounting_journal_lines",
            new ShardContext(FiscalYear: 1405));

        Assert.Equal("accounting_journal_lines_1405", tableName);
    }

    [Fact]
    public void ResolveTableName_Should_Throw_When_FiscalYear_Strategy_And_FiscalYear_Is_Missing()
    {
        var moduleDbResolver = Substitute.For<IModuleDatabaseResolver>();
        moduleDbResolver.GetShardingConfig("Accounting")
            .Returns(new ModuleShardingConfig
            {
                Enabled = true,
                Strategy = "FiscalYear"
            });

        var resolver = new DefaultShardResolver(moduleDbResolver);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.ResolveTableName(
                "Accounting",
                "accounting_journal_lines",
                new ShardContext()));

        Assert.Contains("FiscalYear", exception.Message);
    }
}
