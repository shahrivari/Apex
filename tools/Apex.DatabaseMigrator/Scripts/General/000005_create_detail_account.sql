CREATE TABLE detail_account (
    id bigint NOT NULL,
    code nvarchar(50) NOT NULL,
    name nvarchar(200) NOT NULL,
    type varchar(10) NOT NULL,
    status varchar(10) NOT NULL,
    created_at datetime2(7) NOT NULL,
    updated_at datetime2(7) NULL,
    archived_at datetime2(7) NULL,
    CONSTRAINT pk_detail_account PRIMARY KEY (id),
    CONSTRAINT uq_detail_account_code UNIQUE (code),
    CONSTRAINT ck_detail_account_type CHECK (type IN ('PERSON','SYMBOL','BANK')),
    CONSTRAINT ck_detail_account_status CHECK (status IN ('ACTIVE','ARCHIVED')),
    CONSTRAINT ck_detail_account_archive_time CHECK ((status='ACTIVE' AND archived_at IS NULL) OR (status='ARCHIVED' AND archived_at IS NOT NULL))
);
GO
CREATE INDEX ix_detail_account_type_status_code ON detail_account(type,status,code) INCLUDE(name);
GO
CREATE INDEX ix_detail_account_status_code ON detail_account(status,code) INCLUDE(name,type);
GO
CREATE TABLE detail_account_retired_code (
    code nvarchar(50) NOT NULL,
    retired_at datetime2(7) NOT NULL,
    CONSTRAINT pk_detail_account_retired_code PRIMARY KEY (code)
);
GO
