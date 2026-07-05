namespace Apex.Infrastructure.Data;

using System.Data.Common;
using Apex.Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlReadDbConnectionFactory : IReadDbConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly IModuleDatabaseResolver _moduleDatabaseResolver;

    public SqlReadDbConnectionFactory(
        IConfiguration configuration,
        IModuleDatabaseResolver moduleDatabaseResolver)
    {
        _configuration = configuration;
        _moduleDatabaseResolver = moduleDatabaseResolver;
    }

    public async Task<DbConnection> OpenConnectionAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        var connectionStringName = _moduleDatabaseResolver.GetReadConnectionStringName(moduleName);

        var connectionString = _configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' is missing.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
