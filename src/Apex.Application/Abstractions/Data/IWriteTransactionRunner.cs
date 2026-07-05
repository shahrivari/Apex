namespace Apex.Application.Abstractions.Data;

public interface IWriteTransactionRunner
{
    Task ExecuteAsync(
        string moduleName,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(
        string moduleName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
