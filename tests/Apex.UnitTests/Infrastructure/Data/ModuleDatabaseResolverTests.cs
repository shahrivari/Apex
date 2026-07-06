namespace Apex.UnitTests.Infrastructure.Data;

using Apex.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

public sealed class ModuleDatabaseResolverTests
{
    [Fact]
    public void GetReadConnectionStringName_Should_Return_Module_Read_Connection_Name()
    {
        var configuration = CreateConfiguration();
        var resolver = new ModuleDatabaseResolver(configuration);

        var result = resolver.GetReadConnectionStringName("Accounting");

        Assert.Equal("AccountingReadDb", result);
    }

    [Fact]
    public void GetWriteConnectionStringName_Should_Return_Module_Write_Connection_Name()
    {
        var configuration = CreateConfiguration();
        var resolver = new ModuleDatabaseResolver(configuration);

        var result = resolver.GetWriteConnectionStringName("Accounting");

        Assert.Equal("AccountingWriteDb", result);
    }

    [Fact]
    public void GetShardingConfig_Should_Return_Module_Sharding_Config()
    {
        var configuration = CreateConfiguration();
        var resolver = new ModuleDatabaseResolver(configuration);

        var result = resolver.GetShardingConfig("Accounting");

        Assert.True(result.Enabled);
        Assert.Equal("FiscalYear", result.Strategy);
        Assert.Equal("Current", result.DefaultShard);
    }

    [Fact]
    public void Missing_Module_Config_Should_Throw()
    {
        var configuration = CreateConfiguration();
        var resolver = new ModuleDatabaseResolver(configuration);

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.GetReadConnectionStringName("MissingModule"));

        Assert.Contains("MissingModule", exception.Message);
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Modules:Accounting:Database:ReadConnectionStringName"] = "AccountingReadDb",
            ["Modules:Accounting:Database:WriteConnectionStringName"] = "AccountingWriteDb",
            ["Modules:Accounting:Database:Sharding:Enabled"] = "true",
            ["Modules:Accounting:Database:Sharding:Strategy"] = "FiscalYear",
            ["Modules:Accounting:Database:Sharding:DefaultShard"] = "Current"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
