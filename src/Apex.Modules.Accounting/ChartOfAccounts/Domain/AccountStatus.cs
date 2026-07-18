namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public enum AccountStatus
{
    Active,
    Archived
}

internal static class AccountStatusExtensions
{
    internal static string ToDatabaseValue(this AccountStatus value) => value.ToString().ToUpperInvariant();

    internal static AccountStatus ToAccountStatus(this string value) => Enum.Parse<AccountStatus>(value, true);
}
