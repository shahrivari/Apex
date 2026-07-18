namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public enum DetailAccountType
{
    None,
    Bank,
    Symbol,
    Person
}

internal static class DetailAccountTypeExtensions
{
    internal static string ToDatabaseValue(this DetailAccountType value) => value.ToString().ToUpperInvariant();

    internal static DetailAccountType ToDetailAccountType(this string value) =>
        Enum.Parse<DetailAccountType>(value, true);
}
