namespace Apex.Modules.Accounting.AccountingBooks.Repositories;

using Apex.Modules.Accounting.AccountingBooks.Domain;

public interface IAccountingBookWriteRepository
{
    Task InsertAsync(
        AccountingBook book,
        CancellationToken cancellationToken = default);

    Task<AccountingBook?> GetByIdForUpdateAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeForUpdateAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByOwnerForUpdateAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        AccountingBook book,
        CancellationToken cancellationToken = default);
}
