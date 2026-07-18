namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

internal sealed record SubsidiaryAccountRow(
    long Id,
    long GeneralAccountId,
    string Code,
    string Name,
    string Nature,
    string DetailAccountType,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
