using Apex.Application.Abstractions.Data;
using Dapper;
using Apex.Modules.Accounting.AccountingBooks.Repositories.Rows;

namespace Apex.Modules.Accounting.AccountingBooks.Repositories;

public sealed class AccountingBookReadRepository : IAccountingBookReadRepository
{
    private readonly IGeneralConnectionFactory _connectionFactory;

    public AccountingBookReadRepository(IGeneralConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AccountingBookRow?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory
            .OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<AccountingBookRow>(
            new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    code AS Code,
                    title AS Title,
                    owner_type AS OwnerType,
                    owner_id AS OwnerId,
                    status AS Status,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    activated_at AS ActivatedAt,
                    suspended_at AS SuspendedAt,
                    archived_at AS ArchivedAt
                FROM accounting_book
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<AccountingBookRow?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory
            .OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<AccountingBookRow>(
            new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    code AS Code,
                    title AS Title,
                    owner_type AS OwnerType,
                    owner_id AS OwnerId,
                    status AS Status,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    activated_at AS ActivatedAt,
                    suspended_at AS SuspendedAt,
                    archived_at AS ArchivedAt
                FROM accounting_book
                WHERE code = @Code
                """,
                new { Code = code },
                cancellationToken: cancellationToken));
    }

    public async Task<AccountingBookRow?> GetByOwnerAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory
            .OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<AccountingBookRow>(
            new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    code AS Code,
                    title AS Title,
                    owner_type AS OwnerType,
                    owner_id AS OwnerId,
                    status AS Status,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    activated_at AS ActivatedAt,
                    suspended_at AS SuspendedAt,
                    archived_at AS ArchivedAt
                FROM accounting_book
                WHERE owner_type = @OwnerType AND owner_id = @OwnerId
                """,
                new { OwnerType = ownerType, OwnerId = ownerId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory
            .OpenAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM accounting_book
                WHERE code = @Code
                """,
                new { Code = code },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> ExistsByOwnerAsync(
        string ownerType,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory
            .OpenAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM accounting_book
                WHERE owner_type = @OwnerType AND owner_id = @OwnerId
                """,
                new { OwnerType = ownerType, OwnerId = ownerId },
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<(IReadOnlyList<AccountingBookRow> Items, int TotalCount)> ListAsync(
        string? status = null,
        string? ownerType = null,
        string? ownerId = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        int skip = (page - 1) * pageSize;

        var parameters = new DynamicParameters();
        var whereParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            whereParts.Add("status = @Status");
            parameters.Add("Status", status);
        }

        if (!string.IsNullOrWhiteSpace(ownerType))
        {
            whereParts.Add("owner_type = @OwnerType");
            parameters.Add("OwnerType", ownerType);
        }

        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            whereParts.Add("owner_id = @OwnerId");
            parameters.Add("OwnerId", ownerId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereParts.Add("(code LIKE @Search OR title LIKE @Search)");
            parameters.Add("Search", $"%{search}%");
        }

        string whereClause = whereParts.Count > 0
            ? "WHERE " + string.Join(" AND ", whereParts)
            : "";

        string countSql = $"SELECT COUNT(*) FROM accounting_book {whereClause}";
        string itemsSql = $@"
            SELECT
                id AS Id,
                code AS Code,
                title AS Title,
                owner_type AS OwnerType,
                owner_id AS OwnerId,
                status AS Status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                activated_at AS ActivatedAt,
                suspended_at AS SuspendedAt,
                archived_at AS ArchivedAt
            FROM accounting_book
            {whereClause}
            ORDER BY created_at DESC, id DESC
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY";

        parameters.Add("Skip", skip);
        parameters.Add("PageSize", pageSize);

        var connection = await _connectionFactory
            .OpenAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        var rows = (await connection.QueryAsync<AccountingBookRow>(
            new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken)))
            .ToList();

        return (rows, totalCount);
    }
}
