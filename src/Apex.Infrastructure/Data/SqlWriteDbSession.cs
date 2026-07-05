namespace Apex.Infrastructure.Data;

using System.Data.Common;
using Apex.Application.Abstractions.Data;

public sealed class SqlWriteDbSession : IWriteDbSession
{
    private readonly IWriteDbConnectionFactory _connectionFactory;

    private DbConnection? _connection;
    private DbTransaction? _transaction;

    public SqlWriteDbSession(IWriteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public DbConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "Write DB session has not been opened. Use IWriteTransactionRunner for write operations.");

    public DbTransaction? Transaction => _transaction;

    public string? ModuleName { get; private set; }

    public bool HasActiveTransaction => _transaction is not null;

    public async Task BeginTransactionAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A write transaction is already active.");

        ModuleName = moduleName;

        _connection ??= await _connectionFactory.OpenConnectionAsync(moduleName, cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active write transaction to commit.");

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
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        ModuleName = null;
    }
}
