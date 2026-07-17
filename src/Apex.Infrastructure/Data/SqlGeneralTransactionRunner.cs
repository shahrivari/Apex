namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;

public sealed class SqlGeneralTransactionRunner : IGeneralTransactionRunner
{
    private readonly IGeneralConnectionFactory _connectionFactory;

    public SqlGeneralTransactionRunner(IGeneralConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (_connectionFactory.HasActiveTransaction)
            return await action(cancellationToken);

        await _connectionFactory.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await _connectionFactory.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _connectionFactory.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<T> ExecuteStandaloneAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (_connectionFactory.HasActiveTransaction)
        {
            throw new InvalidOperationException(
                "A standalone general transaction cannot start while another general transaction is active.");
        }

        await _connectionFactory.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await _connectionFactory.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _connectionFactory.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
