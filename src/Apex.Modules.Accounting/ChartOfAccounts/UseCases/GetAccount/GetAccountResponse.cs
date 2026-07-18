namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.GetAccount;

internal sealed record GetAccountResponse(
    long Id, string Level, long? ParentId, string Code, string Name, string? Nature, string? DetailAccountType,
    string Status, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? ArchivedAt);
