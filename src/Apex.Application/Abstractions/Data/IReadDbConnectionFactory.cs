namespace Apex.Application.Abstractions.Data;

using System.Data.Common;

public interface IReadDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(
        string moduleName,
        CancellationToken cancellationToken = default);
}
