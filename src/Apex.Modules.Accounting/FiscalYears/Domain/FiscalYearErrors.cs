namespace Apex.Modules.Accounting.FiscalYears.Domain;

public static class FiscalYearErrors
{
    public const string NotFound = "fiscal_year_not_found";
    public const string InvalidDateRange = "fiscal_year_invalid_date_range";
    public const string DatesOverlap = "fiscal_year_dates_overlap";
    public const string DatesHaveGap = "fiscal_year_dates_have_gap";
    public const string CannotBeUpdated = "fiscal_year_cannot_be_updated";
    public const string CannotBeDeleted = "fiscal_year_cannot_be_deleted";
    public const string CannotBeOpened = "fiscal_year_cannot_be_opened";
    public const string OpenAlreadyExists = "fiscal_year_open_already_exists";
    public const string NotFoundForDate = "fiscal_year_not_found_for_date";
    public const string DateFinalized = "fiscal_year_date_finalized";
    public const string CannotBeFinalized = "fiscal_year_cannot_be_finalized";
    public const string CannotBeCancelled = "fiscal_year_cannot_be_cancelled";
    public const string CannotBeClosed = "fiscal_year_cannot_be_closed";
    public const string CannotAllocateNumber = "fiscal_year_cannot_allocate_number";
    public const string AccountingBookArchived = "fiscal_year_accounting_book_archived";
    public const string AccountingBookNotActive = "fiscal_year_accounting_book_not_active";
}
