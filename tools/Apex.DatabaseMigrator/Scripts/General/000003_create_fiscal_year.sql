CREATE TABLE fiscal_year (
    id BIGINT NOT NULL PRIMARY KEY,
    accounting_book_id BIGINT NOT NULL,
    title NVARCHAR(256) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    status VARCHAR(16) NOT NULL,
    finalized_through_date DATE NOT NULL,
    next_document_number BIGINT NOT NULL,
    created_at DATETIME2(3) NOT NULL,
    updated_at DATETIME2(3) NULL,
    opened_at DATETIME2(3) NULL,
    closed_at DATETIME2(3) NULL,
    cancelled_at DATETIME2(3) NULL,
    cancellation_date DATE NULL,
    CONSTRAINT fk_fiscal_year_accounting_book
        FOREIGN KEY (accounting_book_id) REFERENCES accounting_book (id),
    CONSTRAINT ck_fiscal_year_title_not_empty
        CHECK (LEN(LTRIM(RTRIM(title))) > 0),
    CONSTRAINT ck_fiscal_year_date_range
        CHECK (start_date <= end_date),
    CONSTRAINT ck_fiscal_year_status
        CHECK (status IN ('DRAFT', 'OPEN', 'CLOSED', 'CANCELLED')),
    CONSTRAINT ck_fiscal_year_next_document_number
        CHECK (next_document_number >= 1),
    CONSTRAINT ck_fiscal_year_finalized_range
        CHECK (finalized_through_date >= DATEADD(DAY, -1, start_date)
            AND finalized_through_date <= ISNULL(cancellation_date, end_date)),
    CONSTRAINT ck_fiscal_year_cancellation
        CHECK ((status = 'CANCELLED' AND cancellation_date IS NOT NULL AND cancelled_at IS NOT NULL)
            OR (status <> 'CANCELLED' AND cancellation_date IS NULL AND cancelled_at IS NULL)),
    CONSTRAINT ck_fiscal_year_opened
        CHECK ((status = 'DRAFT' AND opened_at IS NULL)
            OR (status IN ('OPEN', 'CLOSED') AND opened_at IS NOT NULL)
            OR status = 'CANCELLED')
);

CREATE INDEX ix_fiscal_year_book_dates
    ON fiscal_year (accounting_book_id, start_date, end_date);

CREATE INDEX ix_fiscal_year_book_status_dates
    ON fiscal_year (accounting_book_id, status, start_date, end_date);

CREATE UNIQUE INDEX ux_fiscal_year_one_open_per_book
    ON fiscal_year (accounting_book_id)
    WHERE status = 'OPEN';
