namespace Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccount;

public sealed record GetDetailAccountResponse(
    long Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
