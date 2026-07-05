namespace Apex.Infrastructure.Ids;

using Apex.Application.Abstractions.Ids;
using TSID.Creator.NET;

public sealed class TsidIdGenerator : IIdGenerator
{
    public long NewId()
    {
        return TsidCreator.GetTsid().ToLong();
    }
}
