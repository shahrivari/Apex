namespace Apex.Modules.Accounting.DetailAccounts.UseCases.GetDetailAccountByCode;

public sealed record GetDetailAccountByCodeResponse(
    long Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt
);
