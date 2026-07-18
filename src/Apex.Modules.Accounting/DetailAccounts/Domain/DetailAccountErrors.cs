namespace Apex.Modules.Accounting.DetailAccounts.Domain;

public static class DetailAccountErrors
{
    public const string NotFound = "detail_account_not_found";
    public const string CodeAlreadyExists = "detail_account_code_already_exists";
    public const string InvalidCode = "detail_account_invalid_code";
    public const string InvalidName = "detail_account_invalid_name";
    public const string TypeNotSupported = "detail_account_type_not_supported";
    public const string CodeImmutable = "detail_account_code_immutable";
    public const string AlreadyActive = "detail_account_already_active";
    public const string AlreadyArchived = "detail_account_already_archived";
    public const string Archived = "detail_account_archived";
    public const string TypeMismatch = "detail_account_type_mismatch";
    public const string NotAllowed = "detail_account_not_allowed";
    public const string Required = "detail_account_required";
    public const string CannotBeDeleted = "detail_account_cannot_be_deleted";
}
