namespace Apex.Application.Abstractions.Data;

public interface IShardResolver
{
    string ResolveTableName(
        string moduleName,
        string logicalTableName,
        ShardContext context);
}

public sealed record ShardContext(
    int? FiscalYear = null,
    DateOnly? Date = null,
    string? Tenant = null);
