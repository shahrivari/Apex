CREATE TABLE fiscal_year_directory (
    id BIGINT NOT NULL PRIMARY KEY,
    accounting_book_id BIGINT NOT NULL,
    title NVARCHAR(256) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    status VARCHAR(16) NOT NULL,
    finalized_through_date DATE NOT NULL,
    created_at DATETIME2(3) NOT NULL,
    updated_at DATETIME2(3) NULL,
    opened_at DATETIME2(3) NULL,
    closed_at DATETIME2(3) NULL,
    cancelled_at DATETIME2(3) NULL,
    cancellation_date DATE NULL,
    directory_synced_at DATETIME2(3) NOT NULL,
    CONSTRAINT fk_fiscal_year_directory_accounting_book
        FOREIGN KEY (accounting_book_id) REFERENCES accounting_book (id),
    CONSTRAINT ck_fiscal_year_directory_title_not_empty
        CHECK (LEN(LTRIM(RTRIM(title))) > 0),
    CONSTRAINT ck_fiscal_year_directory_date_range
        CHECK (start_date <= end_date),
    CONSTRAINT ck_fiscal_year_directory_status
        CHECK (status IN ('DRAFT', 'OPEN', 'CLOSED', 'CANCELLED')),
    CONSTRAINT ck_fiscal_year_directory_finalized_range
        CHECK (finalized_through_date >= DATEADD(DAY, -1, start_date)
            AND finalized_through_date <= ISNULL(cancellation_date, end_date)),
    CONSTRAINT ck_fiscal_year_directory_cancellation
        CHECK ((status = 'CANCELLED' AND cancellation_date IS NOT NULL AND cancelled_at IS NOT NULL)
            OR (status <> 'CANCELLED' AND cancellation_date IS NULL AND cancelled_at IS NULL)),
    CONSTRAINT ck_fiscal_year_directory_opened
        CHECK ((status = 'DRAFT' AND opened_at IS NULL)
            OR (status IN ('OPEN', 'CLOSED', 'CANCELLED') AND opened_at IS NOT NULL))
);

CREATE INDEX ix_fiscal_year_directory_book_dates
    ON fiscal_year_directory (accounting_book_id, start_date, end_date);

CREATE INDEX ix_fiscal_year_directory_book_status_dates
    ON fiscal_year_directory (accounting_book_id, status, start_date, end_date);
