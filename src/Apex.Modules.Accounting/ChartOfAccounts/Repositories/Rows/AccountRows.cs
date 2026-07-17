namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

internal sealed record AccountClassRow(long Id,string Code,string Name,string Status,DateTime CreatedAt,DateTime? UpdatedAt,DateTime? ArchivedAt);
internal sealed record GeneralAccountRow(long Id,long AccountClassId,string Code,string Name,string Nature,string Status,DateTime CreatedAt,DateTime? UpdatedAt,DateTime? ArchivedAt);
internal sealed record SubsidiaryAccountRow(long Id,long GeneralAccountId,string Code,string Name,string Nature,string DetailAccountType,string Status,DateTime CreatedAt,DateTime? UpdatedAt,DateTime? ArchivedAt);
