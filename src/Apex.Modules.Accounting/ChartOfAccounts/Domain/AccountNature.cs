namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public enum AccountNature
{
    Debtor,
    Creditor,
    Neutral
}

internal static class AccountNatureExtensions
{
    internal static string ToDatabaseValue(this AccountNature value) => value.ToString().ToUpperInvariant();

    internal static AccountNature ToAccountNature(this string value) => Enum.Parse<AccountNature>(value, true);
}
