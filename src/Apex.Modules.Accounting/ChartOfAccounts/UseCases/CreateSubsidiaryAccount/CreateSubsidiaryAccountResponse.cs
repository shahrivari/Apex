namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;

internal sealed record CreateSubsidiaryAccountResponse(
    long Id, long GeneralAccountId, string Code, string Name, string Nature, string DetailAccountType, string Status);
