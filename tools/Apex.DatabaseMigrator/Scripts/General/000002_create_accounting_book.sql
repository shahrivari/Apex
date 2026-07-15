CREATE TABLE accounting_book (
                                 id BIGINT NOT NULL PRIMARY KEY,
                                 code VARCHAR(64) NOT NULL,
                                 title NVARCHAR(256) NOT NULL,
                                 owner_type VARCHAR(64) NOT NULL,
                                 owner_id VARCHAR(128) NOT NULL,
                                 status VARCHAR(32) NOT NULL,
                                 created_at DATETIME2(3) NOT NULL,
                                 updated_at DATETIME2(3) NULL,
                                 activated_at DATETIME2(3) NULL,
                                 suspended_at DATETIME2(3) NULL,
                                 archived_at DATETIME2(3) NULL,
                                 CONSTRAINT ux_accounting_book_code UNIQUE (code),
                                 CONSTRAINT ux_accounting_book_owner UNIQUE (owner_type, owner_id),
                                 CONSTRAINT ck_accounting_book_status
                                     CHECK (status IN ('DRAFT', 'ACTIVE', 'SUSPENDED', 'ARCHIVED')),
                                 CONSTRAINT ck_accounting_book_code_not_empty CHECK (LEN(LTRIM(RTRIM(code))) > 0),
                                 CONSTRAINT ck_accounting_book_title_not_empty CHECK (LEN(LTRIM(RTRIM(title))) > 0),
                                 CONSTRAINT ck_accounting_book_owner_type_not_empty CHECK (LEN(LTRIM(RTRIM(owner_type))) > 0),
                                 CONSTRAINT ck_accounting_book_owner_id_not_empty CHECK (LEN(LTRIM(RTRIM(owner_id))) > 0)
);

CREATE INDEX ix_accounting_book_status ON accounting_book (status);
CREATE INDEX ix_accounting_book_owner ON accounting_book (owner_type, owner_id);
