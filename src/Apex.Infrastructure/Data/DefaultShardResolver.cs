namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;

public sealed class DefaultShardResolver : IShardResolver
{
    private readonly IModuleDatabaseResolver _moduleDatabaseResolver;

    public DefaultShardResolver(IModuleDatabaseResolver moduleDatabaseResolver)
    {
        _moduleDatabaseResolver = moduleDatabaseResolver;
    }

    public string ResolveTableName(
        string moduleName,
        string logicalTableName,
        ShardContext context)
    {
        var sharding = _moduleDatabaseResolver.GetShardingConfig(moduleName);

        if (!sharding.Enabled)
            return logicalTableName;

        if (string.Equals(sharding.Strategy, "FiscalYear", StringComparison.OrdinalIgnoreCase))
        {
            if (context.FiscalYear is null)
                throw new InvalidOperationException(
                    $"FiscalYear shard strategy requires FiscalYear in ShardContext for module '{moduleName}'.");

            return $"{logicalTableName}_{context.FiscalYear.Value}";
        }

        throw new NotSupportedException(
            $"Sharding strategy '{sharding.Strategy}' is not supported for module '{moduleName}'.");
    }
}
