namespace Apex.Application.Abstractions.Data;

public sealed class ModuleDatabaseOptions
{
    public Dictionary<string, ModuleDatabaseConfig> Modules { get; init; } = new();
}

public sealed class ModuleDatabaseConfig
{
    public string ReadConnectionStringName { get; init; } = "";
    public string WriteConnectionStringName { get; init; } = "";
    public ModuleShardingConfig Sharding { get; init; } = new();
}

public sealed class ModuleShardingConfig
{
    public bool Enabled { get; init; }
    public string Strategy { get; init; } = "None";
    public string? DefaultShard { get; init; }
}
