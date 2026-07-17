namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public enum AccountLevel { AccountClass, GeneralAccount, SubsidiaryAccount }
public enum AccountStatus { Active, Archived }
public enum AccountNature { Debtor, Creditor, Neutral }
public enum DetailAccountType { None, Bank, Symbol, Person }

internal static class AccountEnumExtensions
{
    internal static string ToDatabaseValue(this AccountStatus value) => value.ToString().ToUpperInvariant();
    internal static string ToDatabaseValue(this AccountNature value) => value.ToString().ToUpperInvariant();
    internal static string ToDatabaseValue(this DetailAccountType value) => value.ToString().ToUpperInvariant();
    internal static AccountStatus ToAccountStatus(this string value) => Enum.Parse<AccountStatus>(value, true);
    internal static AccountNature ToAccountNature(this string value) => Enum.Parse<AccountNature>(value, true);
    internal static DetailAccountType ToDetailAccountType(this string value) => Enum.Parse<DetailAccountType>(value, true);
}
