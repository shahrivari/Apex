CREATE TABLE accounting_fiscal_years (
    id BIGINT NOT NULL PRIMARY KEY,
    title NVARCHAR(100) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    status INT NOT NULL,
    created_at DATETIME2(3) NOT NULL,
    updated_at DATETIME2(3) NULL
);

CREATE UNIQUE INDEX ux_accounting_fiscal_years_start_end
ON accounting_fiscal_years(start_date, end_date);
