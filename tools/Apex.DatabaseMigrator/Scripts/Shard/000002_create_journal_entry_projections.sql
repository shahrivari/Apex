-- Financial reporting projections for Journal Entries. Derived, disposable, and rebuildable
-- read models; only posted FINANCIAL entries contribute. Both projections are shard-resident so
-- they stay atomic with posting. No-detail rows use an empty-string sentinel (never NULL) so the
-- logical grain uniqueness holds under SQL NULL semantics.

CREATE TABLE daily_account_turnover (
    accounting_book_id BIGINT NOT NULL,
    fiscal_year_id BIGINT NOT NULL,
    balance_date DATE NOT NULL,
    account_class_code NVARCHAR(64) NOT NULL,
    general_account_code NVARCHAR(2) NOT NULL,
    subsidiary_account_code NVARCHAR(2) NOT NULL,
    detail_account_code NVARCHAR(50) NOT NULL,
    document_type VARCHAR(32) NOT NULL,
    debit_turnover DECIMAL(19, 4) NOT NULL,
    credit_turnover DECIMAL(19, 4) NOT NULL,
    net_turnover AS (debit_turnover - credit_turnover) PERSISTED,
    updated_at DATETIME2(3) NOT NULL,
    projection_version INT NOT NULL,
    CONSTRAINT pk_daily_account_turnover PRIMARY KEY (
        accounting_book_id, fiscal_year_id, balance_date, account_class_code,
        general_account_code, subsidiary_account_code, detail_account_code, document_type),
    CONSTRAINT ck_daily_account_turnover_debit CHECK (debit_turnover >= 0),
    CONSTRAINT ck_daily_account_turnover_credit CHECK (credit_turnover >= 0)
);
GO
CREATE INDEX ix_daily_account_turnover_typed
    ON daily_account_turnover (fiscal_year_id, balance_date, document_type);
GO
-- Sparse per-date net movement (debit positive, credit negative). The closing balance as of a
-- date is the running SUM of net_change through that date for the grain.
CREATE TABLE daily_account_balance (
    accounting_book_id BIGINT NOT NULL,
    fiscal_year_id BIGINT NOT NULL,
    account_class_code NVARCHAR(64) NOT NULL,
    general_account_code NVARCHAR(2) NOT NULL,
    subsidiary_account_code NVARCHAR(2) NOT NULL,
    detail_account_code NVARCHAR(50) NOT NULL,
    balance_date DATE NOT NULL,
    net_change DECIMAL(19, 4) NOT NULL,
    updated_at DATETIME2(3) NOT NULL,
    projection_version INT NOT NULL,
    CONSTRAINT pk_daily_account_balance PRIMARY KEY (
        accounting_book_id, fiscal_year_id, account_class_code, general_account_code,
        subsidiary_account_code, detail_account_code, balance_date)
);
GO
CREATE INDEX ix_daily_account_balance_date
    ON daily_account_balance (fiscal_year_id, balance_date);
GO
