using Apex.Modules.Accounting.DetailAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.DetailAccounts.Repositories;

public interface IDetailAccountReadRepository
{
    Task<DetailAccountRow?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<DetailAccountRow?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<DetailAccountRow> Items, int TotalCount)> ListAsync(
        string? type,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<DetailAccountRow>> SearchForPostingAsync(
        string type,
        string? search,
        int limit,
        CancellationToken ct = default
    );
}
