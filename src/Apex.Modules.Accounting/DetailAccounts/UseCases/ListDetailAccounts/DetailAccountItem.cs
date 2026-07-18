namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ListDetailAccounts;

public sealed record DetailAccountItem(
    long Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
