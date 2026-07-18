namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

internal sealed record GeneralAccountRow(
    long Id,
    long AccountClassId,
    string Code,
    string Name,
    string Nature,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
