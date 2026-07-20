namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

internal sealed record AccountPathRow(
    string ClassStatus,
    string GeneralStatus,
    string SubsidiaryStatus,
    string DetailAccountType);
