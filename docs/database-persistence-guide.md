# Apex Database and Persistence Guide

This guide defines the database and persistence conventions for Apex.

Apex uses:

- Dapper only
- SQL Server
- DbUp migrations
- explicit read/write separation
- explicit transaction boundaries
- TSID `BIGINT` identifiers
- module-aware database resolution
- Testcontainers for integration testing

Apex does **not** use EF Core or an ORM.

---

## 1. Core principles

Persistence in Apex follows these rules:

1. Use Dapper for all SQL access.
2. Keep SQL inside repositories.
3. Separate read repositories from write repositories.
4. Queries use read connections.
5. Commands use write transactions.
6. Schema is managed by DbUp migrations.
7. IDs are generated in the application using TSID.
8. Database access is module-aware.
9. Tests use real SQL Server containers.
10. No repository should hide transaction boundaries.

---

## 2. Persistence abstractions

Apex uses these application-level abstractions:

```text
Apex.Application/
  Abstractions/
    Data/
      IReadDbConnectionFactory.cs
      IWriteDbConnectionFactory.cs
      IWriteDbSession.cs
      IWriteTransactionRunner.cs
      IModuleDatabaseResolver.cs
      IShardResolver.cs
      ModuleDatabaseOptions.cs

    Ids/
      IIdGenerator.cs

    Time/
      IClock.cs
```

Modules depend on these abstractions, not on infrastructure implementation details.

---

## 3. Read/write separation

Apex separates read and write access explicitly.

```text
Queries  -> IReadDbConnectionFactory -> Read database
Commands -> IWriteTransactionRunner + IWriteDbSession -> Write database
```

For normal business integration tests and local development, read and write connection strings may point to the same physical database.

For infrastructure tests or production read replicas, they may point to different databases.

---

## 4. Module database configuration

Each module has its own database configuration.

Example:

```json
{
  "Modules": {
    "Accounting": {
      "Database": {
        "ReadConnectionStringName": "AccountingReadDb",
        "WriteConnectionStringName": "AccountingWriteDb",
        "Sharding": {
          "Enabled": true,
          "Strategy": "FiscalYear",
          "DefaultShard": "Current"
        }
      }
    }
  },
  "ConnectionStrings": {
    "AccountingReadDb": "Server=localhost;Database=ApexAccounting;TrustServerCertificate=True;Max Pool Size=200;",
    "AccountingWriteDb": "Server=localhost;Database=ApexAccounting;TrustServerCertificate=True;Max Pool Size=100;"
  }
}
```

The module uses the module name to resolve the correct read/write connection string.

Example:

```csharp
await readDbConnectionFactory.OpenConnectionAsync(AccountingModule.Name, cancellationToken);
```

Avoid hardcoding connection string names inside repositories.

---

## 5. Module name constants

Each module should expose a constant module name.

Example:

```csharp
namespace Apex.Modules.Accounting;

public static class AccountingModule
{
    public const string Name = "Accounting";
}
```

Use this constant for:

```csharp
IReadDbConnectionFactory
IWriteTransactionRunner
IShardResolver
```

Do not repeat `"Accounting"` as a raw string everywhere.

---

## 6. Read repository rule

Read repositories use:

```csharp
IReadDbConnectionFactory
```

They are used by query handlers.

They should contain read-only methods:

```text
GetByIdAsync
ListAsync
SearchAsync
ExistsAsync
GetCurrentAsync
```

They must not mutate state.

They must not depend on:

```csharp
IWriteDbSession
IWriteTransactionRunner
```

Example:

```csharp
public sealed class FiscalYearReadRepository
{
    private readonly IReadDbConnectionFactory _connectionFactory;

    public FiscalYearReadRepository(IReadDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<FiscalYear?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory
            .OpenConnectionAsync(AccountingModule.Name, cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<FiscalYear>(
            new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    title AS Title,
                    start_date AS StartDate,
                    end_date AS EndDate,
                    status AS Status,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM accounting_fiscal_years
                WHERE id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }
}
```

---

## 7. Write repository rule

Write repositories use:

```csharp
IWriteDbSession
```

They are used by command handlers.

They should contain write-side operations and transactionally consistent checks:

```text
InsertAsync
UpdateAsync
DeleteAsync
OpenAsync
CloseAsync
GetByIdForUpdateAsync
ExistsOverlappingAsync
ExistsForUpdateAsync
```

They should not open their own write connection.

They should use:

```csharp
_session.Connection
_session.Transaction
```

Example:

```csharp
public sealed class FiscalYearWriteRepository
{
    private readonly IWriteDbSession _session;

    public FiscalYearWriteRepository(IWriteDbSession session)
    {
        _session = session;
    }

    public async Task InsertAsync(
        FiscalYear fiscalYear,
        CancellationToken cancellationToken = default)
    {
        await _session.Connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO accounting_fiscal_years (
                    id,
                    title,
                    start_date,
                    end_date,
                    status,
                    created_at,
                    updated_at
                )
                VALUES (
                    @Id,
                    @Title,
                    @StartDate,
                    @EndDate,
                    @Status,
                    @CreatedAt,
                    @UpdatedAt
                )
                """,
                fiscalYear,
                transaction: _session.Transaction,
                cancellationToken: cancellationToken));
    }
}
```

---

## 8. Transaction boundary rule

Only command handlers start transactions.

Use:

```csharp
IWriteTransactionRunner
```

Example:

```csharp
await _transactionRunner.ExecuteAsync(AccountingModule.Name, async ct =>
{
    await _writeRepository.InsertAsync(entity, ct);
}, cancellationToken);
```

Repositories must not start, commit, or rollback transactions.

This keeps transaction boundaries visible at the use-case level.

---

## 9. Command-side consistency checks

If a command needs a database-backed check that must be correct inside the transaction, use the write repository.

Examples:

```text
checking overlapping fiscal years
checking duplicate account code
checking journal debit/credit balance before posting
checking whether fiscal year is open
checking whether journal entry can be reversed
```

Good:

```csharp
await _transactionRunner.ExecuteAsync(AccountingModule.Name, async ct =>
{
    var overlaps = await _writeRepository.ExistsOverlappingAsync(
        request.StartDate,
        request.EndDate,
        ct);

    if (overlaps)
    {
        throw new InvalidOperationException("Fiscal year overlaps with an existing fiscal year.");
    }

    await _writeRepository.InsertAsync(fiscalYear, ct);
}, cancellationToken);
```

Avoid using read repositories for command-side checks that must be transactionally consistent.

---

## 10. Query handler rule

Query handlers use read repositories.

Example:

```csharp
public sealed class GetFiscalYearHandler
{
    private readonly FiscalYearReadRepository _readRepository;

    public GetFiscalYearHandler(FiscalYearReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<FiscalYearResponse?> HandleAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await _readRepository.GetByIdAsync(id, cancellationToken);

        return fiscalYear is null
            ? null
            : new FiscalYearResponse(
                fiscalYear.Id,
                fiscalYear.Title,
                fiscalYear.StartDate,
                fiscalYear.EndDate,
                fiscalYear.Status);
    }
}
```

Query handlers should not use write sessions or write repositories.

---

## 11. Command handler rule

Command handlers use write repositories and transaction runner.

Example:

```csharp
public sealed class CreateFiscalYearHandler
{
    private readonly IWriteTransactionRunner _transactionRunner;
    private readonly FiscalYearWriteRepository _writeRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly IValidator<CreateFiscalYearRequest> _validator;

    public async Task<CreateFiscalYearResponse> HandleAsync(
        CreateFiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        CreateFiscalYearResponse? response = null;

        await _transactionRunner.ExecuteAsync(AccountingModule.Name, async ct =>
        {
            var fiscalYear = new FiscalYear
            {
                Id = _idGenerator.NewId(),
                Title = request.Title.Trim(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = FiscalYearStatus.Draft,
                CreatedAt = _clock.UtcNow
            };

            await _writeRepository.InsertAsync(fiscalYear, ct);

            response = new CreateFiscalYearResponse(
                fiscalYear.Id,
                fiscalYear.Title,
                fiscalYear.StartDate,
                fiscalYear.EndDate,
                fiscalYear.Status);
        }, cancellationToken);

        return response!;
    }
}
```

---

## 12. Dapper conventions

Use `CommandDefinition` for cancellation-token support.

Preferred:

```csharp
await connection.QueryAsync<T>(
    new CommandDefinition(
        sql,
        parameters,
        transaction: transaction,
        cancellationToken: cancellationToken));
```

Avoid:

```csharp
await connection.QueryAsync<T>(sql, parameters);
```

Use named parameters.

Good:

```sql
WHERE id = @Id
```

Avoid string interpolation for SQL values.

Bad:

```csharp
$"WHERE id = {id}"
```

Never concatenate user input into SQL.

---

## 13. SQL formatting conventions

Use multiline raw string literals for SQL.

Example:

```csharp
"""
SELECT
    id AS Id,
    title AS Title,
    start_date AS StartDate,
    end_date AS EndDate
FROM accounting_fiscal_years
WHERE id = @Id
"""
```

Prefer explicit column lists.

Avoid:

```sql
SELECT *
```

Always alias snake_case database columns to PascalCase C# properties.

Example:

```sql
created_at AS CreatedAt
```

---

## 14. Table naming

Use module-prefixed table names.

Examples:

```text
accounting_fiscal_years
accounting_accounts
accounting_journal_entries
accounting_journal_entry_lines
```

Avoid generic names:

```text
fiscal_years
accounts
documents
items
```

Module prefixes make the database easier to navigate and reduce collision risk.

---

## 15. Column naming

Use snake_case in SQL Server tables.

Examples:

```text
id
title
start_date
end_date
created_at
updated_at
created_by
updated_by
```

Map to PascalCase in C# using SQL aliases.

---

## 16. ID strategy

Apex uses TSID-generated `BIGINT` IDs.

Database column:

```sql
id BIGINT NOT NULL PRIMARY KEY
```

C# property:

```csharp
public long Id { get; init; }
```

Generate IDs in application code:

```csharp
var id = _idGenerator.NewId();
```

Do not use SQL Server identity columns for domain entities.

Avoid:

```sql
id BIGINT IDENTITY(1,1)
```

TSID benefits:

- sortable by time
- globally generated in application
- works well with distributed services later
- compact `BIGINT` storage

---

## 17. Date and time

Use `DateOnly` for business dates.

Examples:

```csharp
DateOnly StartDate
DateOnly EndDate
```

Use `DateTime` in UTC for timestamps.

Examples:

```csharp
DateTime CreatedAt
DateTime? UpdatedAt
```

Use `IClock` instead of directly calling:

```csharp
DateTime.UtcNow
```

Good:

```csharp
CreatedAt = _clock.UtcNow
```

---

## 18. Connection pooling

`Microsoft.Data.SqlClient` provides connection pooling automatically.

When a repository does this:

```csharp
await using var connection = await _connectionFactory.OpenConnectionAsync(
    moduleName,
    cancellationToken);
```

disposing the connection returns it to the pool.

Rules:

1. Open connections late.
2. Dispose connections early.
3. Do not keep read connections in fields.
4. Do not share `DbConnection` across unrelated operations.
5. Write sessions keep one connection for the transaction scope.

Connection pools are per exact connection string.

If read and write connection strings are different:

```text
AccountingReadDb  -> read pool
AccountingWriteDb -> write pool
```

If they are identical:

```text
AccountingReadDb and AccountingWriteDb -> same physical pool
```

Useful connection string options:

```text
Max Pool Size=200;
Min Pool Size=5;
Connect Timeout=30;
TrustServerCertificate=True;
```

Do not over-tune pool sizes early.

---

## 19. Read connection factory

The read factory:

1. resolves the module's read connection string name
2. reads the actual connection string from configuration
3. creates a `SqlConnection`
4. opens it
5. returns it to the caller

The caller owns disposal.

Example use:

```csharp
await using var connection = await _readDbConnectionFactory
    .OpenConnectionAsync(AccountingModule.Name, cancellationToken);
```

---

## 20. Write DB session

`IWriteDbSession` owns one write connection and one optional transaction for the current scope.

It exposes:

```csharp
DbConnection Connection { get; }
DbTransaction? Transaction { get; }
bool HasActiveTransaction { get; }
```

Write repositories use the current session.

They do not create their own connection.

---

## 21. Write transaction runner

`IWriteTransactionRunner` starts and completes write transactions.

It should:

1. begin transaction
2. execute the command delegate
3. commit on success
4. rollback on failure

It also supports nested command execution by detecting an already-active transaction.

Rule:

```text
Command handlers call transaction runner.
Write repositories use write session.
```

---

## 22. Nested transaction behavior

If a transaction is already active, the transaction runner should not start a new transaction.

This allows one command handler to call internal code that also expects transaction support.

Expected behavior:

```text
No active transaction -> begin + commit/rollback
Active transaction    -> reuse current session
```

Do not use SQL Server nested transactions unless there is a clear need.

---

## 23. Isolation level

Default transaction isolation should be SQL Server default unless a use case requires stricter behavior.

For accounting commands, consider explicit isolation when needed:

```text
ReadCommitted
RepeatableRead
Serializable
Snapshot
```

Use stricter isolation only for commands that require it, such as:

```text
posting journal entries
closing fiscal years
preventing overlapping fiscal years
allocating sequence numbers
```

Do not globally raise isolation level without measurement.

---

## 24. Locking guidance

Use database constraints first.

Use transaction isolation or locks only when constraints are not enough.

Examples:

- Unique account code: use unique index.
- Fiscal year overlap: may require transaction + range lock or serialized command.
- Journal posting: use transaction and status checks.
- Idempotency key: use unique index.

Prefer deterministic database constraints over application-only checks.

---

## 25. Error handling

Repositories should usually let database exceptions bubble up.

Handlers may convert known business violations into domain/application exceptions.

Examples:

```text
Duplicate account code -> business conflict
Fiscal year overlap -> business conflict
Journal not balanced -> validation/business error
```

Do not swallow SQL exceptions in repositories.

Do not return false for unexpected database errors.

---

## 26. DbUp migrations

Apex uses DbUp for schema migrations.

Migration files live under:

```text
tools/Apex.DatabaseMigrator/Scripts/<ModuleName>/
```

Example:

```text
tools/Apex.DatabaseMigrator/Scripts/Accounting/
  000001_create_accounting_fiscal_years.sql
  000002_create_accounting_accounts.sql
  000003_create_accounting_journal_entries.sql
```

Rules:

1. One migration file per meaningful schema change.
2. Never edit an already-applied migration.
3. Add a new migration for changes.
4. Keep migrations deterministic.
5. Use explicit table and index names.
6. Do not create schema in tests manually.

---

## 27. Migration naming

Use this format:

```text
000001_create_accounting_fiscal_years.sql
000002_create_accounting_accounts.sql
000003_create_accounting_journal_entries.sql
000004_add_accounting_accounts_unique_code.sql
```

Keep ordering numeric and stable.

---

## 28. Migration content conventions

Good migration:

```sql
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
```

Prefer explicit names:

```sql
ux_accounting_fiscal_years_start_end
ix_accounting_journal_entries_fiscal_year_id
fk_accounting_journal_lines_entry_id
```

---

## 29. Constraints and indexes

Use constraints and indexes to protect important invariants.

Examples:

```text
unique account code
unique fiscal year date range if applicable
journal entry status indexes
foreign keys from journal lines to journal entries
indexes on fiscal year/date/status columns
```

Application checks are not enough for data integrity.

---

## 30. Sharding hooks

Apex has sharding abstractions:

```csharp
IShardResolver
ShardContext
```

Current supported strategy:

```text
FiscalYear
```

The logical table name can be resolved to a physical table name.

Example:

```csharp
var tableName = _shardResolver.ResolveTableName(
    AccountingModule.Name,
    "accounting_journal_entry_lines",
    new ShardContext(FiscalYear: fiscalYear));
```

This may produce:

```text
accounting_journal_entry_lines_1405
```

Do not manually concatenate shard table names in handlers.

Sharding decisions belong in persistence infrastructure/repositories.

---

## 31. Sharding rule

Only use sharding where data volume requires it.

Likely candidates:

```text
journal entries
journal entry lines
daily balances
ledger movements
```

Do not shard small reference tables:

```text
fiscal years
chart of accounts
account types
statuses
```

Start simple. Add sharding when volume requires it.

---

## 32. Integration testing rules

Business integration tests use one physical database.

```text
AccountingReadDb == AccountingWriteDb
```

Base class:

```csharp
ApexIntegrationTestBase
```

Use this for normal business tests so reads observe writes.

Infrastructure read/write separation tests use two physical databases.

```text
AccountingReadDb != AccountingWriteDb
```

Base class:

```csharp
SeparatedReadWriteIntegrationTestBase
```

Use this only to test routing and infrastructure behavior.

---

## 33. Test schema rule

Tests must not manually create tables.

Schema comes from DbUp migrations.

Allowed in tests:

```text
insert test data
delete test data
assert persisted state
```

Not allowed in tests:

```text
CREATE TABLE
ALTER TABLE
CREATE INDEX
```

Those belong in migrations.

---

## 34. Test data reset

Each test should reset the data it mutates.

Use fixture reset helpers, for example:

```csharp
await ResetAccountingDatabaseAsync();
```

Reset methods should:

- delete test rows
- preserve schema
- not rerun migrations per test
- not restart containers per test

---

## 35. Repository testing

Repository integration tests should use real SQL Server through Testcontainers.

Do not mock Dapper.

Do not mock `SqlConnection`.

For read repositories:

```text
arrange data with direct SQL
call read repository
assert result
```

For write repositories:

```text
start write transaction runner
call write repository
assert committed/rolled back state
```

---

## 36. Unit testing

Unit tests are appropriate for:

```text
domain methods
validators
pure mapping logic
handler branching with mocked repositories
```

Do not use unit tests to prove SQL correctness.

SQL correctness belongs in integration tests.

---

## 37. Common anti-patterns

Avoid these:

```text
SQL in handlers
SQL in endpoints
SQL in validators
repositories starting transactions
commands using read repositories for critical checks
query handlers using write sessions
manual schema creation in tests
EF Core mixed with Dapper
string interpolation in SQL
SELECT *
identity columns for domain IDs
generic Services folder for persistence logic
```

---

## 38. Recommended module persistence layout

Example for a capability:

```text
FiscalYears/
  Domain/
    FiscalYear.cs
    FiscalYearStatus.cs

  Repositories/
    FiscalYearReadRepository.cs
    FiscalYearWriteRepository.cs

  CreateFiscalYear/
    CreateFiscalYearRequest.cs
    CreateFiscalYearResponse.cs
    CreateFiscalYearValidator.cs
    CreateFiscalYearHandler.cs

  GetFiscalYear/
    GetFiscalYearResponse.cs
    GetFiscalYearHandler.cs

  ListFiscalYears/
    ListFiscalYearsRequest.cs
    ListFiscalYearsResponse.cs
    ListFiscalYearsHandler.cs
```

---

## 39. Recommended handler-to-repository mapping

```text
CreateFiscalYearHandler -> FiscalYearWriteRepository
OpenFiscalYearHandler   -> FiscalYearWriteRepository
CloseFiscalYearHandler  -> FiscalYearWriteRepository

GetFiscalYearHandler    -> FiscalYearReadRepository
ListFiscalYearsHandler  -> FiscalYearReadRepository
```

This mapping should be obvious from constructor dependencies.

---

## 40. Final rules

1. Dapper only.
2. SQL lives in repositories.
3. Read and write repositories are separate.
4. Queries use read repositories.
5. Commands use write repositories.
6. Commands start transactions through `IWriteTransactionRunner`.
7. Write repositories use `IWriteDbSession`.
8. Read repositories use `IReadDbConnectionFactory`.
9. IDs are TSID `BIGINT`, generated in application code.
10. Schema is managed by DbUp.
11. Tests use DbUp migrations, not manual schema creation.
12. Use one DB for business integration tests.
13. Use two DBs only for read/write infrastructure tests.
14. Keep connection lifetime short.
15. Let ADO.NET connection pooling do its job.
