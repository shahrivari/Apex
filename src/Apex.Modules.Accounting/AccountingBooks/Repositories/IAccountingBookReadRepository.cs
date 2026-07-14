namespace Apex.Modules.Accounting.AccountingBooks.Repositories;

using Apex.Modules.Accounting.AccountingBooks.Repositories.Rows;

public interface IAccountingBookReadRepository
{
    Task<AccountingBookRow?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<AccountingBookRow?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<AccountingBookRow?> GetByOwnerAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByOwnerAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AccountingBookRow> Items, int TotalCount)> ListAsync(
        string? status = null,
        string? ownerType = null,
        string? ownerId = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
