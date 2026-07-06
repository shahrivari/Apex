IF OBJECT_ID('db_marker', 'U') IS NULL
BEGIN
    CREATE TABLE db_marker (
        name NVARCHAR(100) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM db_marker)
BEGIN
    INSERT INTO db_marker(name) VALUES ('UNSET_DATABASE');
END

IF OBJECT_ID('write_transaction_test', 'U') IS NULL
BEGIN
    CREATE TABLE write_transaction_test (
        id INT NOT NULL PRIMARY KEY,
        name NVARCHAR(100) NOT NULL
    );
END
