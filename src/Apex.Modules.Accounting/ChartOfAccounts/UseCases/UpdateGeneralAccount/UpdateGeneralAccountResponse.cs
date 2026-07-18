namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateGeneralAccount;

internal sealed record UpdateGeneralAccountResponse(
    long Id, long AccountClassId, string Code, string Name, string Nature, string Status);
