namespace Apex.Application.Abstractions.Data;

public interface IModuleDatabaseResolver
{
    string GetReadConnectionStringName(string moduleName);
    string GetWriteConnectionStringName(string moduleName);
    ModuleShardingConfig GetShardingConfig(string moduleName);
}
