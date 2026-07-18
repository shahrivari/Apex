using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateSubsidiaryAccount;

internal sealed record CreateSubsidiaryAccountRequest(
    long GeneralAccountId, string Code, string Name, AccountNature Nature, DetailAccountType DetailAccountType);
