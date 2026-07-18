namespace Apex.Modules.Accounting.DetailAccounts.Repositories.Rows;

public sealed record DetailAccountRow(
    long Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
