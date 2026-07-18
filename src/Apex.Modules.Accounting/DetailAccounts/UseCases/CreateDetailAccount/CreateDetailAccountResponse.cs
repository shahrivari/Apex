namespace Apex.Modules.Accounting.DetailAccounts.UseCases.CreateDetailAccount;

public sealed record CreateDetailAccountResponse(
    long Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTime CreatedAt
);
