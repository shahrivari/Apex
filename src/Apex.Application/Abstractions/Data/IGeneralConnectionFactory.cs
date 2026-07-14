namespace Apex.Application.Abstractions.Data;

using System.Data.Common;

public interface IGeneralConnectionFactory : IAsyncDisposable
{
    DbTransaction? Transaction { get; }
    bool HasActiveTransaction { get; }

    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IGeneralTransactionRunner
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
