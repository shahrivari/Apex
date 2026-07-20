CREATE TABLE fiscal_year (
    id BIGINT NOT NULL,
    accounting_book_id BIGINT NOT NULL,
    title NVARCHAR(256) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    status VARCHAR(16) NOT NULL,
    finalized_through_date DATE NOT NULL,
    next_reference_number BIGINT NOT NULL,
    next_journal_entry_number BIGINT NOT NULL,
    created_at DATETIME2(3) NOT NULL,
    updated_at DATETIME2(3) NULL,
    opened_at DATETIME2(3) NULL,
    closed_at DATETIME2(3) NULL,
    cancelled_at DATETIME2(3) NULL,
    cancellation_date DATE NULL,
    CONSTRAINT pk_fiscal_year PRIMARY KEY (id),
    CONSTRAINT uq_fiscal_year_id_book UNIQUE (id, accounting_book_id),
    CONSTRAINT ck_fiscal_year_title CHECK (LEN(LTRIM(RTRIM(title))) > 0),
    CONSTRAINT ck_fiscal_year_dates CHECK (start_date <= end_date),
    CONSTRAINT ck_fiscal_year_status CHECK (status IN ('DRAFT', 'OPEN', 'CLOSED', 'CANCELLED')),
    CONSTRAINT ck_fiscal_year_counters CHECK (
        next_reference_number >= 1 AND next_journal_entry_number >= 1),
    CONSTRAINT ck_fiscal_year_finalized CHECK (
        finalized_through_date >= DATEADD(DAY, -1, start_date)
        AND finalized_through_date <= ISNULL(cancellation_date, end_date))
);
GO
CREATE TABLE journal_entry (
    id BIGINT NOT NULL,
    accounting_book_id BIGINT NOT NULL,
    fiscal_year_id BIGINT NOT NULL,
    reference_number BIGINT NOT NULL,
    journal_entry_number BIGINT NOT NULL,
    number_finalized BIT NOT NULL,
    accounting_date DATE NOT NULL,
    registered_at DATETIME2(3) NOT NULL,
    description NVARCHAR(1024) NOT NULL,
    document_type VARCHAR(32) NOT NULL,
    insertion_type VARCHAR(16) NOT NULL,
    status VARCHAR(16) NOT NULL,
    balance_effect VARCHAR(16) NOT NULL,
    source_type VARCHAR(64) NULL,
    source_reference NVARCHAR(200) NULL,
    reversal_of_reference_number BIGINT NULL,
    reversed_by_reference_number BIGINT NULL,
    reversal_reason NVARCHAR(1024) NULL,
    posted_at DATETIME2(3) NULL,
    created_at DATETIME2(3) NOT NULL,
    updated_at DATETIME2(3) NULL,
    CONSTRAINT pk_journal_entry PRIMARY KEY (id),
    CONSTRAINT fk_journal_entry_fiscal_year FOREIGN KEY (fiscal_year_id, accounting_book_id)
        REFERENCES fiscal_year (id, accounting_book_id),
    CONSTRAINT uq_journal_entry_reference
        UNIQUE (accounting_book_id, fiscal_year_id, reference_number),
    CONSTRAINT uq_journal_entry_number
        UNIQUE (accounting_book_id, fiscal_year_id, journal_entry_number),
    CONSTRAINT ck_journal_entry_status CHECK (status IN ('DRAFT', 'POSTED')),
    CONSTRAINT ck_journal_entry_document_type CHECK (document_type IN (
        'GENERAL', 'OPENING', 'CLOSING',
        'TEMPORARY_ACCOUNTS_CLOSING', 'PERFORMANCE_ACCOUNTS_CLOSING')),
    CONSTRAINT ck_journal_entry_insertion_type CHECK (insertion_type IN (
        'MANUAL', 'SEMI_SYSTEM', 'SYSTEM', 'MIGRATION')),
    CONSTRAINT ck_journal_entry_balance_effect CHECK (balance_effect IN ('FINANCIAL', 'STATISTICAL')),
    CONSTRAINT ck_journal_entry_description CHECK (LEN(LTRIM(RTRIM(description))) > 0),
    CONSTRAINT ck_journal_entry_reference_positive CHECK (reference_number >= 1),
    CONSTRAINT ck_journal_entry_number_positive CHECK (journal_entry_number >= 1),
    CONSTRAINT ck_journal_entry_posted_at CHECK (
        (status = 'DRAFT' AND posted_at IS NULL)
        OR (status = 'POSTED' AND posted_at IS NOT NULL)),
    CONSTRAINT ck_journal_entry_source_pair CHECK (
        (source_type IS NULL AND source_reference IS NULL)
        OR (source_type IS NOT NULL AND source_reference IS NOT NULL))
);
GO
CREATE UNIQUE INDEX ux_journal_entry_source
    ON journal_entry (fiscal_year_id, source_type, source_reference)
    WHERE source_reference IS NOT NULL;
GO
CREATE INDEX ix_journal_entry_finalization_order
    ON journal_entry (fiscal_year_id, accounting_date, registered_at, reference_number);
GO
CREATE INDEX ix_journal_entry_book_fy_status
    ON journal_entry (accounting_book_id, fiscal_year_id, status);
GO
CREATE TABLE journal_entry_line (
    id BIGINT NOT NULL,
    journal_entry_id BIGINT NOT NULL,
    row_number INT NOT NULL,
    account_class_code NVARCHAR(64) NOT NULL,
    general_account_code NVARCHAR(2) NOT NULL,
    subsidiary_account_code NVARCHAR(2) NOT NULL,
    detail_account_code NVARCHAR(50) NULL,
    side VARCHAR(8) NOT NULL,
    amount DECIMAL(19, 4) NOT NULL,
    description NVARCHAR(1024) NOT NULL,
    CONSTRAINT pk_journal_entry_line PRIMARY KEY (id),
    CONSTRAINT fk_journal_entry_line_entry
        FOREIGN KEY (journal_entry_id) REFERENCES journal_entry (id) ON DELETE CASCADE,
    CONSTRAINT uq_journal_entry_line_row
        UNIQUE (journal_entry_id, row_number),
    CONSTRAINT ck_journal_entry_line_side CHECK (side IN ('DEBIT', 'CREDIT')),
    CONSTRAINT ck_journal_entry_line_amount CHECK (amount > 0),
    CONSTRAINT ck_journal_entry_line_row_positive CHECK (row_number >= 1),
    CONSTRAINT ck_journal_entry_line_codes CHECK (
        LEN(LTRIM(RTRIM(account_class_code))) > 0
        AND LEN(LTRIM(RTRIM(general_account_code))) > 0
        AND LEN(LTRIM(RTRIM(subsidiary_account_code))) > 0),
    CONSTRAINT ck_journal_entry_line_description CHECK (LEN(LTRIM(RTRIM(description))) > 0)
);
GO
