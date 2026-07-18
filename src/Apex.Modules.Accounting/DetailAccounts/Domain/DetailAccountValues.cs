using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.DetailAccounts.Domain;

public static class DetailAccountValues
{
    public static string ToDatabaseValue(this DetailAccountType value) =>
        value.ToString().ToUpperInvariant();

    public static string ToDatabaseValue(this DetailAccountStatus value) =>
        value.ToString().ToUpperInvariant();

    public static DetailAccountType ParseType(string value) =>
        Enum.TryParse<DetailAccountType>(value, true, out var result)
            ? result
            : throw new BusinessRuleException(
                "Detail account type is not supported.",
                DetailAccountErrors.TypeNotSupported
            );

    public static DetailAccountStatus ParseStatus(string value) =>
        Enum.TryParse<DetailAccountStatus>(value, true, out var result)
            ? result
            : throw new BusinessRuleException(
                "Detail account status is invalid.",
                "detail_account_invalid_status"
            );
}
