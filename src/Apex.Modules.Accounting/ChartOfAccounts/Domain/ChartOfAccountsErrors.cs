namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

internal static class ChartOfAccountsErrors
{
    internal const string AccountClassNotFound = "account_class_not_found";
    internal const string GeneralAccountNotFound = "general_account_not_found";
    internal const string SubsidiaryAccountNotFound = "subsidiary_account_not_found";
    internal const string CodeAlreadyExists = "account_code_already_exists";
    internal const string ParentInactive = "account_parent_inactive";
    internal const string HasActiveChildren = "account_has_active_children";
    internal const string CannotBeChanged = "account_cannot_be_changed";
    internal const string AlreadyActive = "account_already_active";
    internal const string AlreadyArchived = "account_already_archived";
}
