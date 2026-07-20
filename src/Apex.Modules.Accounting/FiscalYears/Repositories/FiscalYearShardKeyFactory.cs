using System.Globalization;
using Apex.Application.Abstractions.Data;

namespace Apex.Modules.Accounting.FiscalYears.Repositories;

public sealed class FiscalYearShardKeyFactory : IShardKeyFactory<long>
{
    public const string EntityType = "FiscalYear";

    public ShardKey Create(long fiscalYearId) =>
        new(EntityType, fiscalYearId.ToString(CultureInfo.InvariantCulture));
}
