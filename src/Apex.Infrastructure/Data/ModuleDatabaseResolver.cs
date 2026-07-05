namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Microsoft.Extensions.Configuration;

public sealed class ModuleDatabaseResolver : IModuleDatabaseResolver
{
    private readonly IConfiguration _configuration;

    public ModuleDatabaseResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetReadConnectionStringName(string moduleName)
    {
        return GetModuleConfig(moduleName).ReadConnectionStringName;
    }

    public string GetWriteConnectionStringName(string moduleName)
    {
        return GetModuleConfig(moduleName).WriteConnectionStringName;
    }

    public ModuleShardingConfig GetShardingConfig(string moduleName)
    {
        return GetModuleConfig(moduleName).Sharding;
    }

    private ModuleDatabaseConfig GetModuleConfig(string moduleName)
    {
        var section = _configuration.GetSection($"Modules:{moduleName}:Database");

        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"Database configuration for module '{moduleName}' is missing.");
        }

        var config = section.Get<ModuleDatabaseConfig>();

        if (config is null)
        {
            throw new InvalidOperationException(
                $"Database configuration for module '{moduleName}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(config.ReadConnectionStringName))
        {
            throw new InvalidOperationException(
                $"ReadConnectionStringName for module '{moduleName}' is missing.");
        }

        if (string.IsNullOrWhiteSpace(config.WriteConnectionStringName))
        {
            throw new InvalidOperationException(
                $"WriteConnectionStringName for module '{moduleName}' is missing.");
        }

        return config;
    }
}
