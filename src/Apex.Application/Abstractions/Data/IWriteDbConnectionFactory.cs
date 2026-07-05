namespace Apex.Application.Abstractions.Data;

using System.Data.Common;

public interface IWriteDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(
        string moduleName,
        CancellationToken cancellationToken = default);
}
