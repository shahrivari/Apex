CREATE INDEX ix_journal_entry_report_order
    ON journal_entry (fiscal_year_id, status, accounting_date, registered_at, reference_number)
    INCLUDE (accounting_book_id, journal_entry_number, document_type, insertion_type, balance_effect);
GO
CREATE INDEX ix_journal_entry_line_account_path
    ON journal_entry_line (
        account_class_code, general_account_code, subsidiary_account_code,
        detail_account_code, journal_entry_id, row_number)
    INCLUDE (side, amount, description);
GO
