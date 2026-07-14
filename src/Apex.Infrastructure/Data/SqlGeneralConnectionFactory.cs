namespace Apex.Infrastructure.Data;

using System.Data.Common;
using System.Data;
using Apex.Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlGeneralConnectionFactory : IGeneralConnectionFactory
{
    private readonly IConfiguration _configuration;
    private DbConnection? _connection;
    private DbTransaction? _transaction;

    public SqlGeneralConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DbTransaction? Transaction => _transaction;
    public bool HasActiveTransaction => _transaction is not null;

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.State == ConnectionState.Open)
            return _connection;

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        var options = _configuration.GetSection("Sharding").Get<ShardingOptions>() ?? new ShardingOptions();
        var connectionString = _configuration.GetConnectionString(options.GeneralConnectionStringName)
            ?? throw new InvalidOperationException(
                $"General connection string '{options.GeneralConnectionStringName}' is missing.");

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            _connection = connection;
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A general database transaction is already active.");

        var connection = await OpenAsync(cancellationToken);
        _transaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active general database transaction to commit.");

        await _transaction.CommitAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    private async Task DisposeTransactionAsync()
    {
        await _transaction!.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await DisposeTransactionAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        _connection = null;
    }
}
