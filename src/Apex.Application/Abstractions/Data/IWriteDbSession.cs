namespace Apex.Application.Abstractions.Data;

using System.Data.Common;

public interface IWriteDbSession : IAsyncDisposable
{
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }
    string? ModuleName { get; }
    bool HasActiveTransaction { get; }

    Task BeginTransactionAsync(
        string moduleName,
        CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
