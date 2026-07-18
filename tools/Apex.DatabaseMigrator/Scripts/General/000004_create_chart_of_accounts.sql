CREATE TABLE account_class (
 id BIGINT NOT NULL PRIMARY KEY, code NVARCHAR(64) NOT NULL, name NVARCHAR(255) NOT NULL,
 status VARCHAR(16) NOT NULL, created_at DATETIME2(3) NOT NULL, updated_at DATETIME2(3) NULL, archived_at DATETIME2(3) NULL,
 CONSTRAINT ux_account_class_code UNIQUE(code), CONSTRAINT ck_account_class_code CHECK(LEN(LTRIM(RTRIM(code)))>0),
 CONSTRAINT ck_account_class_name CHECK(LEN(LTRIM(RTRIM(name)))>0), CONSTRAINT ck_account_class_status CHECK(status IN('ACTIVE','ARCHIVED')),
 CONSTRAINT ck_account_class_archival CHECK((status='ACTIVE' AND archived_at IS NULL) OR (status='ARCHIVED' AND archived_at IS NOT NULL)));

CREATE TABLE general_account (
 id BIGINT NOT NULL PRIMARY KEY, account_class_id BIGINT NOT NULL, code NVARCHAR(2) NOT NULL, name NVARCHAR(255) NOT NULL,
 nature VARCHAR(16) NOT NULL, status VARCHAR(16) NOT NULL, created_at DATETIME2(3) NOT NULL, updated_at DATETIME2(3) NULL, archived_at DATETIME2(3) NULL,
 CONSTRAINT fk_general_account_class FOREIGN KEY(account_class_id) REFERENCES account_class(id), CONSTRAINT ux_general_account_parent_code UNIQUE(account_class_id,code),
 CONSTRAINT ck_general_account_code CHECK(LEN(LTRIM(RTRIM(code)))>0), CONSTRAINT ck_general_account_name CHECK(LEN(LTRIM(RTRIM(name)))>0),
 CONSTRAINT ck_general_account_nature CHECK(nature IN('DEBTOR','CREDITOR','NEUTRAL')), CONSTRAINT ck_general_account_status CHECK(status IN('ACTIVE','ARCHIVED')),
 CONSTRAINT ck_general_account_archival CHECK((status='ACTIVE' AND archived_at IS NULL) OR (status='ARCHIVED' AND archived_at IS NOT NULL)));

CREATE TABLE subsidiary_account (
 id BIGINT NOT NULL PRIMARY KEY, general_account_id BIGINT NOT NULL, code NVARCHAR(2) NOT NULL, name NVARCHAR(255) NOT NULL,
 nature VARCHAR(16) NOT NULL, detail_account_type VARCHAR(16) NOT NULL, status VARCHAR(16) NOT NULL, created_at DATETIME2(3) NOT NULL, updated_at DATETIME2(3) NULL, archived_at DATETIME2(3) NULL,
 CONSTRAINT fk_subsidiary_general FOREIGN KEY(general_account_id) REFERENCES general_account(id), CONSTRAINT ux_subsidiary_parent_code UNIQUE(general_account_id,code),
 CONSTRAINT ck_subsidiary_code CHECK(LEN(LTRIM(RTRIM(code)))>0), CONSTRAINT ck_subsidiary_name CHECK(LEN(LTRIM(RTRIM(name)))>0),
 CONSTRAINT ck_subsidiary_nature CHECK(nature IN('DEBTOR','CREDITOR','NEUTRAL')), CONSTRAINT ck_subsidiary_detail_type CHECK(detail_account_type IN('NONE','BANK','SYMBOL','PERSON')),
 CONSTRAINT ck_subsidiary_status CHECK(status IN('ACTIVE','ARCHIVED')), CONSTRAINT ck_subsidiary_archival CHECK((status='ACTIVE' AND archived_at IS NULL) OR (status='ARCHIVED' AND archived_at IS NOT NULL)));
CREATE INDEX ix_general_account_parent_status ON general_account(account_class_id,status);
CREATE INDEX ix_subsidiary_parent_status ON subsidiary_account(general_account_id,status);
