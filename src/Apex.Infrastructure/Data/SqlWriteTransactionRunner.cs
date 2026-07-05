namespace Apex.Infrastructure.Data;

using Apex.Application.Abstractions.Data;

public sealed class SqlWriteTransactionRunner : IWriteTransactionRunner
{
    private readonly IWriteDbSession _session;

    public SqlWriteTransactionRunner(IWriteDbSession session)
    {
        _session = session;
    }

    public async Task ExecuteAsync(
        string moduleName,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(moduleName, async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        string moduleName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (_session.HasActiveTransaction)
        {
            return await action(cancellationToken);
        }

        await _session.BeginTransactionAsync(moduleName, cancellationToken);

        try
        {
            var result = await action(cancellationToken);
            await _session.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _session.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
