namespace Apex.Modules.Accounting.JournalEntries.Domain;

public static class JournalEntryErrors
{
    public const string NotFound = "journal_entry_not_found";
    public const string AccountingBookNotEligible = "journal_entry_accounting_book_not_eligible";
    public const string FiscalYearNotFound = "journal_entry_fiscal_year_not_found";
    public const string FiscalYearNotOpen = "journal_entry_fiscal_year_not_open";
    public const string AccountingDateOutsideFiscalYear = "journal_entry_accounting_date_outside_fiscal_year";
    public const string AccountingDateFinalized = "journal_entry_accounting_date_finalized";
    public const string DraftRequired = "journal_entry_draft_required";
    public const string PostedImmutable = "journal_entry_posted_immutable";
    public const string InsufficientLines = "journal_entry_insufficient_lines";
    public const string Unbalanced = "journal_entry_unbalanced";
    public const string InvalidRowNumber = "journal_entry_invalid_row_number";
    public const string DuplicateRowNumber = "journal_entry_duplicate_row_number";
    public const string NonPositiveAmount = "journal_entry_non_positive_amount";
    public const string InvalidAccountCodePath = "journal_entry_invalid_account_code_path";
    public const string AccountNotEligible = "journal_entry_account_not_eligible";
    public const string DetailAccountRequired = "journal_entry_detail_account_required";
    public const string DetailAccountNotAllowed = "journal_entry_detail_account_not_allowed";
    public const string DescriptionRequired = "journal_entry_description_required";
    public const string UnsupportedDocumentType = "journal_entry_unsupported_document_type";
    public const string UnsupportedInsertionType = "journal_entry_unsupported_insertion_type";
    public const string UnsupportedBalanceEffect = "journal_entry_unsupported_balance_effect";
    public const string UnsupportedSide = "journal_entry_unsupported_side";
    public const string UnsupportedStatus = "journal_entry_unsupported_status";
    public const string DetailAccountNotFound = "journal_entry_detail_account_not_found";
    public const string DetailAccountInactive = "journal_entry_detail_account_inactive";
    public const string DetailAccountIncompatible = "journal_entry_detail_account_incompatible";
    public const string DuplicateSourceReference = "journal_entry_duplicate_source_reference";
    public const string ConflictingIdempotentRequest = "journal_entry_conflicting_idempotent_request";
    public const string NumberingConflict = "journal_entry_numbering_conflict";
    public const string AlreadyReversed = "journal_entry_already_reversed";
    public const string InvalidReversalDate = "journal_entry_invalid_reversal_date";
    public const string ReversalReasonRequired = "journal_entry_reversal_reason_required";
    public const string InvalidFinalizationDate = "journal_entry_invalid_finalization_date";
    public const string DraftsBlockFinalization = "journal_entry_drafts_block_finalization";
    public const string ProjectionReconciliationFailed = "journal_entry_projection_reconciliation_failed";
    public const string NotAuthorized = "journal_entry_not_authorized";
}
