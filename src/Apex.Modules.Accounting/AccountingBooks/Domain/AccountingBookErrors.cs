namespace Apex.Modules.Accounting.AccountingBooks.Domain;

public static class AccountingBookErrors
{
    public const string AccountingBookNotFound = "accounting_book_not_found";

    public const string AccountingBookCodeAlreadyExists = "accounting_book_code_already_exists";

    public const string AccountingBookOwnerAlreadyExists = "accounting_book_owner_already_exists";

    public const string AccountingBookCannotBeActivated = "accounting_book_cannot_be_activated";

    public const string AccountingBookCannotBeSuspended = "accounting_book_cannot_be_suspended";

    public const string AccountingBookCannotBeArchived = "accounting_book_cannot_be_archived";
}
