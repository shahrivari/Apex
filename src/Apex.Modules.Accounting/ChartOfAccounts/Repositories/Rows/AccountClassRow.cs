namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

internal sealed record AccountClassRow(
    long Id,
    string Code,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
