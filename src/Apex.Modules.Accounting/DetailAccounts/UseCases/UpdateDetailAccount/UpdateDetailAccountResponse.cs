namespace Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;

public sealed record UpdateDetailAccountResponse(
    long Id,
    string Code,
    string Name,
    string Type,
    string Status,
    DateTime? UpdatedAt
);
