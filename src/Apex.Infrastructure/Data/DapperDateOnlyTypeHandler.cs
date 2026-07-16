using System.Data;
using Dapper;

namespace Apex.Infrastructure.Data;

internal sealed class DapperDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    private static int _registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
            SqlMapper.AddTypeHandler(new DapperDateOnlyTypeHandler());
    }

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        DateOnly dateOnly => dateOnly,
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateOnly.")
    };
}
