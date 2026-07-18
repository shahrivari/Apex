namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;

internal sealed record CreateGeneralAccountResponse(
    long Id, long AccountClassId, string Code, string Name, string Nature, string Status);
