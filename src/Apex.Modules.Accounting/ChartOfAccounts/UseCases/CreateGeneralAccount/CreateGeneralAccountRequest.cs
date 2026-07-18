using Apex.Modules.Accounting.ChartOfAccounts.Domain;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.CreateGeneralAccount;

internal sealed record CreateGeneralAccountRequest(long AccountClassId, string Code, string Name, AccountNature Nature);
