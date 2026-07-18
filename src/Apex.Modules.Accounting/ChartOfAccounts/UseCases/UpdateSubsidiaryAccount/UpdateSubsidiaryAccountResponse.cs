namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.UpdateSubsidiaryAccount;

internal sealed record UpdateSubsidiaryAccountResponse(
    long Id, long GeneralAccountId, string Code, string Name, string Nature, string DetailAccountType, string Status);
